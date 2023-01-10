# PowerToys disk usage footprint review

Starting with the release v0.66 of PowerToys, .NET runtime dlls are being shipped self-contained as a part of PowerToys. .NET dlls are being installed in `<PowerToysInstallDir>/dll/dotnet`. During the installation process, hard-links are being created for every module that needs .NET libraries.

File Explorer treats hard-links the same as "original"/regular files/directories (https://learn.microsoft.com/en-us/troubleshoot/windows-server/backup-and-storage/disk-space-problems-on-ntfs-volumes#other-ntfs-features-that-may-cause-file-allocation-confusion), not as links. This results in File Explorer reporting size of PowerToys installation directory bigger than it is (more than 2GB). Shown bellow is real disk usage footprint of PowerToys v0.66 obtained by installing PowerToys on empty disk:

# Steps

## Empty disk details

<img src="../images/disk-usage/empty_disk_details.png">

## Install PowerToys to empty disk

<img src="../images/disk-usage/PowerToys_install_dir.png">

## PowerToys install directory size shown by File Explorer

As mentioned above, File Explorer shows size of PowerToys installation dir as every hard-link is a regular file for itself

<img src="../images/disk-usage/install_dir_size_v0.66.png">

## PowerToys size shown by App->Installed apps

<img src="../images/disk-usage/add_remove_size_v0.66.png">

## Disk usage with PowerToys installed

Real disk usage of PowerToys is shown by inspecting disk usage after installing PowerToys. Used space is now 695MB, comparing to ~35MB used space for empty disk gives us the size of ~660MB for PowerToys installation dir.

<img src="../images/disk-usage/used_disk_space_v0.66.png">

## PowerShell command calculating size of non-hardlinks files

Size of regular files (non-hard-links) can also be obtanied by running following PowerShell command in PowerToys installation dir:

```
Regular files:
ls -Recurse -File -force -ErrorAction SilentlyContinue | ? LinkType -ne HardLink | Measure-Object -Property Length -Sum

Hard-links
ls -Recurse -File -force -ErrorAction SilentlyContinue | ? LinkType -e HardLink | Measure-Object -Property Length -Sum
```

Running these commands for PowerToys v0.66 shows that size of regular files is way less than size of hard-links pointing to some of those regular files. Not much sense there, right?

<img src="../images/disk-usage/pwsh_v0.66.png">
