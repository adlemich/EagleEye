# Main Intend and key Use Cases

The following describes the main intend and key use cases that shall be implemented by the Eagle-Eye project and software.

## Parental Control for Windows

This is about parental control for kids which are using Windows to play games, do school work and more. The main intend is to enable parents to control which application on Windows the kid can use, get an insight how long the kid uses these apps and then be able to control details like which web pages can be browsed and which time budgets for application the kid has.

## Main Personas

There are two main personas:

1: The parent (father, mother or both, grandparents - who ever is responsible for the kid)

- wants to limit time and application selection for protection of mental health of the kid.
- acts with administrative rights in Windows, by installing the applications the kid can use.
- configures the time budgets and app selection for the kid.
- has full control over what the kid can use and for how long per day and per week.

2: The kid, which wants to play games all day but is limited by whatever the parent configured.

- Acts as a restricted user in Windows (standard user profile).
- Can not install applications alone but requires the parent.
- Gets notifications when a time budget is about to expire to be able to save and close.
- Can not use their applications when the time budget or schedule is exausted.

## Solution Modules to be delivered

1: EagleEye server application

- This is a Windows executable that installs as system service with admin rights
- It is provided as installer for Windows 11, 64 Bits with a UI guided installation wizzard (which provides install, repair, uninstall)
- It runs in the background an monitors the applications used by the kid
- It receives configuration from remote mobile parent applications
- It shows a tray icon that shows basic information to the kid when clicked
- It enforces the configuration from the parent and provides messages to the kid when required
- It optionally shows an overlay information pane (that is always visible, also in games) showing the remaining time
- It collects local statistics for the kids application usage and serves that to mobile apps

2: The EagleEye mobile parent applications

- This app is available for iOS (Apple) and Android mobile devices.
- It operates only in portrait orientation and implements typical look and feel for Android and iOS.
- This app is used by parents to configure the EagleEye server application.
- This app is used by parents to query data and look at statistics provided by the EagleEye server application.
- Both apps are delivered independantly (in the way iOS and Android does it typically).
- The iOS Apps should work with iOS 26 and newer.
- The android App should work with Android 14 and newer.

3: The EagleEye Mac parent application

- This application shall be delivered as native Mac application (Apple Silicon)
- It is functionally equal to the mobile parent applications, but the UI is a desktop application
- Works on MacBooks with MacOS 26 and newer.

## Main Workflow

1. Parent logs into Windows with an administrative local account.
2. Parent installs the EagleEye server application as Windows Service with Admin rights.
3. Parent installs the applications which the kid shall use.
4. Parent creates a standard local user in Windows for the kid.
5. Parent logs out on Windows
6. Parent installs the EagleEye App on their mobile phone or MacBook (Android or iOS)
7. Parent connects to EagleEye mobile app to the EagleEye Server on Windows via local LAN
8. Parent configures which apps the kid can use on Windows by using the mobile app (remote config)
9. Parent configures time budget for each app - for how long it can be used per day in the week
10. Parent configures more details (to be defined later)
11. Kid logs in on Windows
12. The EagleEye server application as Windows enforces the rules which were configured by Parent
13. Kid tries to start a non-allowed application, which is prevented by the EagleEye server application on Windows.
14. Kid opens an allowed application, which works fine.
15. The EagleEye server application as Windows tracks the amount of minutes for each used application by the kid.
16. When the configured time budget is almost exausted, the EagleEye server application on Windows presents a warning to the kid.
17. When the configured time budget is exausted, the EagleEye server application as Windows shuts down the application accordingly.
