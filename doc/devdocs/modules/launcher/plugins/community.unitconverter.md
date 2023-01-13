# Unit Converter Plugin
The Unit Convert plugin as the name suggests is used to perform unit conversion on the user entered query.
This plugin uses a package called [UnitsNet](https://github.com/angularsen/UnitsNet).

![Image of Calculator plugin](/doc/images/launcher/plugins/community.unitconverter.png)

### Currently Supported Units
 - [Acceleration](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/AccelerationUnit.g.cs)
 - [Angle](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/AngleUnit.g.cs)
 - [Area](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/AreaUnit.g.cs)
 - [Duration](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/DurationUnit.g.cs)
 - [Energy](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/EnergyUnit.g.cs)
 - [Information](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/InformationUnit.g.cs)
 - [Length](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/LengthUnit.g.cs)
 - [Mass](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/MassUnit.g.cs)
 - [Power](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/PowerUnit.g.cs)
 - [Pressure](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/PressureUnit.g.cs)
 - [Speed](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/SpeedUnit.g.cs)
 - [Temperature](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/TemperatureUnit.g.cs)
 - [Volume](https://github.com/angularsen/UnitsNet/blob/master/UnitsNet/GeneratedCode/Units/VolumeUnit.g.cs)

 These are the ones that are currently enabled (though UnitsNet supports many more). They are defined in [`Main.cs`](/src/modules/launcher/Plugins/Community.PowerToys.Run.UnitConverter/Main.cs).


### [`InputInterpreter`](/src/modules/launcher/Plugins/Community.PowerToys.Run.UnitConverter/InputInterpreter.cs)
 - Class which manipulates user input such that it may be interpreted correctly and thus converted.
 - Uses a regex amongst other things to do this.

### [`UnitHandler`](/src/modules/launcher/Plugins/Community.PowerToys.Run.UnitConverter/UnitHandler.cs)
 - Class that does the actual conversion.
 - Supports abbreviations in user input (single, double, or none).