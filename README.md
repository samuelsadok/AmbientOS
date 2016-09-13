# AmbientOS

The AmbientOS project attempts to provide a framework to create distributed modular cross-platform Applications.

## Where we're at currently

There is a Visual Studio project system that you can install. Having installed the VS extension, you can create an AmbientOS Application or an AmbientOS Library Project in C#. To launch the application, you can select a target platform. Currently, the only available modes are Windows Console, Windows Service and iOS App (if Xamarin is installed).
An AmbientOS App is linked against the standard AmbientOS C# libraries, where the object manager and platform abstractions reside.

The "kernel" folder contains a bootable micro kernel, including an NTFS bootloader. This is not yet related to the rest of the project, but ideally it should one day be able to load AmbientOS Apps. However, to be useful, it would have to be able to load Linux/Windows modules and executables as well. Note that most of the kernel source code actually resides in "shared-c".

## What this isn't (yet)

This is not an operating system which you can download, install and use. Maybe it will never become a standalone system. But it is very likely that within the next months there will appear a Linux based solution.

## What's up next

The next step will be to write a small Demo application, and to write a Getting Started guide based on that. At the same time, the framework will be extended such that the App runs on Windows, Linux and iOS, without __any__ code changes.
Next, we need to implement all of the connectivity stuff, which is at the core of the AmbientOS concept.

In parallel, we may introduce a script which pulls, compiles and installs a usable Linux system.

Starting with a working Linux system, we can integrate AmbientOS solutions one by one to improve the experience, until the entire system (even the kernel) is assimilated. Realistically, this will not happen. But the great thing with this approach is that we can stop at any point, and still have a working system.

## Related projects

The AmbientOS framework will enable multiple ambitious projects, some of which may never make it

- AmbientDesk, a desktop environment designed for a modern ubiquitous computing experience (some keywords: cross-device, multi-user, mouse/touch/keyboard/gesture-friendly, easy to use and beautiful)
- AmbientDrive, a virtual drive which synchronizes your files among devices, hard drives, your NAS or the cloud of your choice. Using heuristics and preferences, your most relevant files are kept locally on every device, while your historic data is archived only on large drives.
- Unmessy, a UNiversal MEdia Storage SYstem, to keep your photos, videos and music organized, safe and available where you need them (based on AmbientDrive)
- S1, a smartphone controlled pocket drone
