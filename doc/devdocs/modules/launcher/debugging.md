# Debugging
`PowerToys Run` is a single exe file associated with `launcher.exe` process and debugger should be attached to this process. There are two approaches to debug `PowerToys Run`. Both these approaches differ in the compile-time and the range of functionalities that could be debugged. These methods are discussed in detail in the following sections.


## Debugging Prerequisite
Setup development environment for PowerToys by following instruction [here.](https://github.com/microsoft/PowerToys/tree/main/doc/devdocs#prerequisites-for-compiling-powertoys)

## Direct debugging
This approach is used to test UI, plugins, and core `PowerToys Run` functionality. This **cannot** be used to test `PowerToys Run` settings. The approach is significantly faster compared to `Debugging with runner`, as it requires compiling projects relevant to `PowerToys Run`. Please follow the steps below for direct debugging.
1. Right-click on `modules->launcher->PowerLauncher` and select `Set as startup Project`. 
2. Press `F5` to start debugging.

## Debugging with runner
This approach can be used to test UI, plugins, core `PowerToys Run` functionality and `PowerToys Run` settings. This approach **cannot** be used to debug functions that execute on starting `launcher.exe` process. This requires building runner along with all the other modules on first compile, making it slower than `Direct debugging` approach. The subsequent compilations should be fast.
1. Right-click on `runner` and select `Set as startup Project`. 
2. Press `F5` to start debugging.
3. Attach debugger to `launcher.exe` process. 
    1. Go to `Debug->Attach to process..`
    2. Filter and select `launcher.exe` process. 
    3. Click on `Attach`.
