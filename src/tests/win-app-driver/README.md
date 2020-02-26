# PowerToys Tests

The PowerToys tests are implemented using Appium and use the [Windows Application Driver](https://github.com/microsoft/WinAppDriver) as an Appium compatible server for Windows applications.

## Prerequisites
  - Install the Windows Application Driver in the test machine: https://github.com/Microsoft/WinAppDriver/releases
  - Install the ".Net desktop development" components in Visual Studio 2017. It should have support for "C#" and ".Net Framework 4.6.1".

## Running the tests

### Preparing the test machine
  - Start `powertoys.exe`.
  - Set Windows to "Developer Mode", by selecting `Developer mode` in `Settings > For developers > Use developer features` in Windows 10.
  - Run the "Windows Application Driver" in Administrator mode in the test machine.

When testing on a remote machine, Firewall exceptions must be added and the IP and port must be passed when starting "Windows Application Driver". Here's how to do it from the [Windows Application Driver FAQ](https://github.com/microsoft/WinAppDriver/wiki/Frequently-Asked-Questions#running-on-a-remote-machine):

#### Running on a Remote Machine

Windows Application Driver can run remotely on any Windows 10 machine with `WinAppDriver.exe` installed and running. This *test machine* can then serve any JSON wire protocol commands coming from the *test runner* remotely through the network. Below are the steps to the one-time setup for the *test machine* to receive inbound requests:

1. On the *test machine* you want to run the test application on, open up **Windows Firewall with Advanced Security**
   - Select **Inbound Rules** -> **New Rule...**
   - **Rule Type** -> **Port**
   - Select **TCP**
   - Choose specific local port (4723 is WinAppDriver standard)
   - **Action** -> **Allow the connection**
   - **Profile** -> select all
   - **Name** -> optional, choose name for rule (e.g. WinAppDriver remote).

   Below command when run in admin command prompt gives same result
   ```shell
   netsh advfirewall firewall add rule name="WinAppDriver remote" dir=in action=allow protocol=TCP localport=4723
   ```

2. Run `ipconfig.exe` to determine your machine's local IP address
   > **Note**: Setting `*` as the IP address command line option will cause it to bind to all bound IP addresses on the machine
3. Run `WinAppDriver.exe 10.X.X.10 4723/wd/hub` as **administrator** with command line arguments as seen above specifying local IP and port
4. On the *test runner* machine where the runner and scripts are, update the test script to point to the IP of the remote *test machine*

### Starting the tests in the Development Machine
  - Open `powertoys.sln` in Visual Studio 2017.
  - Build the `PowerToysTests` project.
  - Select `Test > Windows > Test Explorer`.
  - Select `Test > Run > All` tests in the menu bar.

> Once the project is successfully built, you can use the **TestExplorer** to pick and choose the test scenario(s) to run

> If Visual Studio fail to discover and run the test scenarios:
> 1. Select **Tools** > **Options...** > **Test**
> 2. Under *Active Solution*, uncheck *For improved performance, only use test adapters in test assembly folder or as specified in runsettings file*

If a remote test machine is being used, the IP of the test machine must be used to replace the `WindowsApplicationDriverUrl` value in [PowerToysSession.cs](PowerToysSession.cs).
