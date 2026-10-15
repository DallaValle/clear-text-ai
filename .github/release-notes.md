## Install

### Windows

1. Download the `windows-x64.zip` below and extract `ClearTextAI.exe` to a folder of your choice.
2. Run `ClearTextAI.exe`.
   The first launch takes a few seconds longer while it unpacks itself.
3. The build is not code signed yet, so SmartScreen may stop the first launch.
   Choose **More info**, then **Run anyway**.
4. On a company managed PC, Microsoft Defender may block it outright with "Access is denied" or a notice that your IT administrator blocked it.
   Your IT team has to allow ClearText AI until releases are code signed.

Needs Windows 10 version 1809 or newer on a 64 bit Intel or AMD PC.
To update, exit ClearText AI from its notification area icon and replace `ClearTextAI.exe`.
Settings, history and your API key are stored separately and carry over.

### macOS

1. Download the `macos.dmg` below, open it and drag **ClearText AI** onto **Applications**.
2. Open ClearText AI from Applications.
   The build is not notarized yet, so macOS refuses the first launch.
3. Open **System Settings > Privacy & Security**, find the message about ClearText AI and choose **Open Anyway**.
4. Select text in any app, press **Cmd+Shift+I** and allow ClearText AI under **Accessibility** when asked.

Runs natively on Apple silicon and Intel Macs with macOS 12 or newer.
Because the build is not signed with a developer certificate, macOS treats every update as a new app.
After updating, remove ClearText AI from **Accessibility** with the minus button and allow it again.
If macOS asks whether ClearText AI may use its saved key in your keychain, choose **Always Allow**.
