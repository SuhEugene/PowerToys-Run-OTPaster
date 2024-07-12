# PowerToys Run: OTPaster plugin

Simple [PowerToys Run](https://learn.microsoft.com/windows/powertoys/run) plugin for easily storing TOTP secrets and pasting TOTP codes.

## Requirements

- PowerToys minimum version 0.77.0

## Installation

- Download the [latest release](https://github.com/SuhEugene/PowerToys-Run-OTPaster/releases/) by selecting the architecture that matches your machine: `x64` (more common) or `ARM64`
- Close PowerToys
- Extract the archive to `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins`
- Open PowerToys

## Usage

### Insert code
- Place cursor where text should be pasted 
- Open PowerToys Run
- Input: `% <service name>`
- Select the result (ENTER)
- Code is pasted into the selected location

### Add service
- Copy the string containing OTP authorization data, it starts with `otpauth://totp/`
- Input `% otpauth://totp/...`
- Choose "Create OTP account"
- Now it's in the list
