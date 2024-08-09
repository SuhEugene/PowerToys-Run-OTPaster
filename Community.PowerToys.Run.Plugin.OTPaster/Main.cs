// Copyright (c) Corey Hayward. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.OTPaster
{
    public class Main : IPlugin, ISettingProvider, IContextMenu
    {
        private PluginInitContext? _context;
        private string? _iconPath;
        private int _beginTypeDelay;
        private int _minRemainingTime;
        public string Name => "OTPaster";
        public string Description => "Searches the OTP codes list and pastes the selected item.";
        public static string PluginID => "2e5ef47a099efd00cba2f38bf01e5ddf";

        private class OTPAuthInfo
        {
            public required string Label { get; set; }
            public required string Secret { get; set; }
        }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = "PasteDelay",
                DisplayLabel = "Paste Delay (ms)",
                DisplayDescription = "How long in milliseconds to wait before paste occurs.",
                NumberValue = 200,
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
            },
            new PluginAdditionalOption()
            {
                Key = "MinRemainingTime",
                DisplayLabel = "Minimal Remaining Time (s)",
                DisplayDescription = "Any code that is valid for less than this amount of time will not be inserted. Instead, a new one will be waited for.",
                NumberValue = 5,
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
            },
        };

        public void Init(PluginInitContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _context = context;
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            var otpEntry = CheckQueryForOTP(query.Search.Trim());

            if (otpEntry != null)
                results.Add(CreateSaveOTPResult(otpEntry));

            var otpAccountItems = GetOTPItemsFromStorage();
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                foreach (var item in otpAccountItems)
                    if (item.Label.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
                        results.Add(CreateResult(item));
            }
            else
            {
                foreach (var item in otpAccountItems)
                    results.Add(CreateResult(item));
            }

            return results;
        }

        private OTPAuthInfo? CheckQueryForOTP(string queryText)
        {
            if (!queryText.StartsWith("otpauth://")) return null;

            var otpAuthRegex = new Regex(@"otpauth:\/\/t?otp\/(?<label>[^?]+)?\?(?<query>[^)]+)", RegexOptions.IgnoreCase);
            var match = otpAuthRegex.Match(queryText);

            if (!match.Success) return null;

            var query = match.Groups["query"].Value;

            string? label = match.Groups["label"].Value;
            string? secret = null;
            string? issuer = null;

            var queryRegex = new Regex(@"(?:&|^)(?<key>[^=]+)=(?<value>[^&]+)", RegexOptions.IgnoreCase);
            var queryMatches = queryRegex.Matches(query);

            foreach (Match queryMatch in queryMatches)
            {
                var key = queryMatch.Groups["key"].Value;
                var value = queryMatch.Groups["value"].Value;

                switch (key)
                {
                    case "issuer":
                        issuer = value;
                        break;
                    case "secret":
                        secret = value;
                        break;
                    case "algorithm":
                        if (!value.Equals("SHA1", StringComparison.OrdinalIgnoreCase))
                            return null;
                        break;
                    case "digits":
                        if (int.TryParse(value, out var digits))
                            if (digits != 6) return null;
                        break;
                    case "period":
                        if (int.TryParse(value, out var period))
                            if (period != 30) return null;
                        break;
                }
            }
            if (secret == null) return null;

            return new OTPAuthInfo()
            {
                Label = issuer == null ? label : $"{issuer}: {label}",
                Secret = secret
            };

        }

        private Result CreateResult(OTPAuthInfo item)
            => new Result()
            {
                Title = item.Label,
                SubTitle = "Paste this account code.",
                IcoPath = _iconPath,
                Action = (context) =>
                {


                    Task.Run(() => RunAsSTAThread(() =>
                    {
                        var timeLeft = TOTPGenerator.GetTimeLeft();
                        if (_beginTypeDelay + _minRemainingTime > timeLeft)
                            Thread.Sleep(timeLeft);

                        var code = TOTPGenerator.GenerateCode(item.Secret);
                        Clipboard.SetText(code);
                        Thread.Sleep(_beginTypeDelay);
                        SendKeys.SendWait(code);
                    }));
                    return true;
                },
                ContextData = item,
            };

        private Result CreateSaveOTPResult(OTPAuthInfo authInfo)
        {
            return new Result()
            {
                Title = $"[+] {authInfo.Label}",
                SubTitle = "Create OTP account",
                IcoPath = _iconPath,
                Action = (context) =>
                {
                    AddOTPItem(authInfo);
                    return true;
                },
                ContextData = authInfo
            };
        }

        private void AddOTPItem(OTPAuthInfo item)
        {
            var items = GetOTPItemsFromStorage();
            items.Add(item);
            WriteOTPItemsToStorage(items);
        }

        private void RemoveOTPItem(OTPAuthInfo item)
        {
            var items = GetOTPItemsFromStorage();
            var index = items.FindIndex(i => i.Label.Equals(item.Label) && i.Secret.Equals(item.Secret));
            if (index == -1) return;
            items.RemoveAt(index);
            WriteOTPItemsToStorage(items);
        }

        private void WriteOTPItemsToStorage(List<OTPAuthInfo> items)
        {
            if (_context?.CurrentPluginMetadata.PluginDirectory == null) throw new Exception("No plugin dir?");
            string dataPath = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "otps.json");

            File.WriteAllText(dataPath, JsonSerializer.Serialize(items));
        }

        private List<OTPAuthInfo> GetOTPItemsFromStorage()
        {
            if (_context?.CurrentPluginMetadata.PluginDirectory == null) throw new Exception("No plugin dir?");
            string dataPath = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "otps.json");

            if (!File.Exists(dataPath))
            {
                File.WriteAllText(dataPath, "[]");
                return [];
            }

            string jsonFromFile = File.ReadAllText(dataPath);

            List<OTPAuthInfo> deserializedAuthInfo = JsonSerializer.Deserialize<List<OTPAuthInfo>>(jsonFromFile) ?? throw new Exception("I have parsed shit!");
            return deserializedAuthInfo;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (selectedResult.Title.StartsWith("[+]")) return new List<ContextMenuResult>();

            return new List<ContextMenuResult>
            {
                new()
                {
                    Title = "Delete code (Ctrl+Shift+Enter)",
                    Glyph = "\xE74D",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    PluginName = Name,
                    Action = _ =>
                    {
                        RemoveOTPItem((OTPAuthInfo)selectedResult.ContextData);
                        return true;
                    },
                },
            };
        }
        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _iconPath = "Images/OTPaster.light.png";
            }
            else
            {
                _iconPath = "Images/OTPaster.dark.png";
            }
        }

        public System.Windows.Controls.Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings?.AdditionalOptions is null)
            {
                return;
            }

            var typeDelay = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "PasteDelay");
            _beginTypeDelay = Math.Clamp((int)(typeDelay?.NumberValue ?? 200), 100, 3000);

            var remainingTime = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "MinRemainingTime");
            _minRemainingTime = Math.Clamp((int)(remainingTime?.NumberValue ?? 5), 2, 10) * 1000;
        }

        /// <summary>
        /// Start an Action within an STA Thread
        /// </summary>
        /// <param name="action">The action to execute in the STA thread</param>
        static void RunAsSTAThread(Action action)
        {
            AutoResetEvent @event = new AutoResetEvent(false);
            Thread thread = new Thread(
                () =>
                {
                    action();
                    @event.Set();
                });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            @event.WaitOne();
        }
    }
}
