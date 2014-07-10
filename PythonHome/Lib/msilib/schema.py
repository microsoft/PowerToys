from . import Table

_Validation = Table('_Validation')
_Validation.add_field(1,'Table',11552)
_Validation.add_field(2,'Column',11552)
_Validation.add_field(3,'Nullable',3332)
_Validation.add_field(4,'MinValue',4356)
_Validation.add_field(5,'MaxValue',4356)
_Validation.add_field(6,'KeyTable',7679)
_Validation.add_field(7,'KeyColumn',5378)
_Validation.add_field(8,'Category',7456)
_Validation.add_field(9,'Set',7679)
_Validation.add_field(10,'Description',7679)

ActionText = Table('ActionText')
ActionText.add_field(1,'Action',11592)
ActionText.add_field(2,'Description',7936)
ActionText.add_field(3,'Template',7936)

AdminExecuteSequence = Table('AdminExecuteSequence')
AdminExecuteSequence.add_field(1,'Action',11592)
AdminExecuteSequence.add_field(2,'Condition',7679)
AdminExecuteSequence.add_field(3,'Sequence',5378)

Condition = Table('Condition')
Condition.add_field(1,'Feature_',11558)
Condition.add_field(2,'Level',9474)
Condition.add_field(3,'Condition',7679)

AdminUISequence = Table('AdminUISequence')
AdminUISequence.add_field(1,'Action',11592)
AdminUISequence.add_field(2,'Condition',7679)
AdminUISequence.add_field(3,'Sequence',5378)

AdvtExecuteSequence = Table('AdvtExecuteSequence')
AdvtExecuteSequence.add_field(1,'Action',11592)
AdvtExecuteSequence.add_field(2,'Condition',7679)
AdvtExecuteSequence.add_field(3,'Sequence',5378)

AdvtUISequence = Table('AdvtUISequence')
AdvtUISequence.add_field(1,'Action',11592)
AdvtUISequence.add_field(2,'Condition',7679)
AdvtUISequence.add_field(3,'Sequence',5378)

AppId = Table('AppId')
AppId.add_field(1,'AppId',11558)
AppId.add_field(2,'RemoteServerName',7679)
AppId.add_field(3,'LocalService',7679)
AppId.add_field(4,'ServiceParameters',7679)
AppId.add_field(5,'DllSurrogate',7679)
AppId.add_field(6,'ActivateAtStorage',5378)
AppId.add_field(7,'RunAsInteractiveUser',5378)

AppSearch = Table('AppSearch')
AppSearch.add_field(1,'Property',11592)
AppSearch.add_field(2,'Signature_',11592)

Property = Table('Property')
Property.add_field(1,'Property',11592)
Property.add_field(2,'Value',3840)

BBControl = Table('BBControl')
BBControl.add_field(1,'Billboard_',11570)
BBControl.add_field(2,'BBControl',11570)
BBControl.add_field(3,'Type',3378)
BBControl.add_field(4,'X',1282)
BBControl.add_field(5,'Y',1282)
BBControl.add_field(6,'Width',1282)
BBControl.add_field(7,'Height',1282)
BBControl.add_field(8,'Attributes',4356)
BBControl.add_field(9,'Text',7986)

Billboard = Table('Billboard')
Billboard.add_field(1,'Billboard',11570)
Billboard.add_field(2,'Feature_',3366)
Billboard.add_field(3,'Action',7474)
Billboard.add_field(4,'Ordering',5378)

Feature = Table('Feature')
Feature.add_field(1,'Feature',11558)
Feature.add_field(2,'Feature_Parent',7462)
Feature.add_field(3,'Title',8000)
Feature.add_field(4,'Description',8191)
Feature.add_field(5,'Display',5378)
Feature.add_field(6,'Level',1282)
Feature.add_field(7,'Directory_',7496)
Feature.add_field(8,'Attributes',1282)

Binary = Table('Binary')
Binary.add_field(1,'Name',11592)
Binary.add_field(2,'Data',2304)

BindImage = Table('BindImage')
BindImage.add_field(1,'File_',11592)
BindImage.add_field(2,'Path',7679)

File = Table('File')
File.add_field(1,'File',11592)
File.add_field(2,'Component_',3400)
File.add_field(3,'FileName',4095)
File.add_field(4,'FileSize',260)
File.add_field(5,'Version',7496)
File.add_field(6,'Language',7444)
File.add_field(7,'Attributes',5378)
File.add_field(8,'Sequence',1282)

CCPSearch = Table('CCPSearch')
CCPSearch.add_field(1,'Signature_',11592)

CheckBox = Table('CheckBox')
CheckBox.add_field(1,'Property',11592)
CheckBox.add_field(2,'Value',7488)

Class = Table('Class')
Class.add_field(1,'CLSID',11558)
Class.add_field(2,'Context',11552)
Class.add_field(3,'Component_',11592)
Class.add_field(4,'ProgId_Default',7679)
Class.add_field(5,'Description',8191)
Class.add_field(6,'AppId_',7462)
Class.add_field(7,'FileTypeMask',7679)
Class.add_field(8,'Icon_',7496)
Class.add_field(9,'IconIndex',5378)
Class.add_field(10,'DefInprocHandler',7456)
Class.add_field(11,'Argument',7679)
Class.add_field(12,'Feature_',3366)
Class.add_field(13,'Attributes',5378)

Component = Table('Component')
Component.add_field(1,'Component',11592)
Component.add_field(2,'ComponentId',7462)
Component.add_field(3,'Directory_',3400)
Component.add_field(4,'Attributes',1282)
Component.add_field(5,'Condition',7679)
Component.add_field(6,'KeyPath',7496)

Icon = Table('Icon')
Icon.add_field(1,'Name',11592)
Icon.add_field(2,'Data',2304)

ProgId = Table('ProgId')
ProgId.add_field(1,'ProgId',11775)
ProgId.add_field(2,'ProgId_Parent',7679)
ProgId.add_field(3,'Class_',7462)
ProgId.add_field(4,'Description',8191)
ProgId.add_field(5,'Icon_',7496)
ProgId.add_field(6,'IconIndex',5378)

ComboBox = Table('ComboBox')
ComboBox.add_field(1,'Property',11592)
ComboBox.add_field(2,'Order',9474)
ComboBox.add_field(3,'Value',3392)
ComboBox.add_field(4,'Text',8000)

CompLocator = Table('CompLocator')
CompLocator.add_field(1,'Signature_',11592)
CompLocator.add_field(2,'ComponentId',3366)
CompLocator.add_field(3,'Type',5378)

Complus = Table('Complus')
Complus.add_field(1,'Component_',11592)
Complus.add_field(2,'ExpType',13570)

Directory = Table('Directory')
Directory.add_field(1,'Directory',11592)
Directory.add_field(2,'Directory_Parent',7496)
Directory.add_field(3,'DefaultDir',4095)

Control = Table('Control')
Control.add_field(1,'Dialog_',11592)
Control.add_field(2,'Control',11570)
Control.add_field(3,'Type',3348)
Control.add_field(4,'X',1282)
Control.add_field(5,'Y',1282)
Control.add_field(6,'Width',1282)
Control.add_field(7,'Height',1282)
Control.add_field(8,'Attributes',4356)
Control.add_field(9,'Property',7474)
Control.add_field(10,'Text',7936)
Control.add_field(11,'Control_Next',7474)
Control.add_field(12,'Help',7986)

Dialog = Table('Dialog')
Dialog.add_field(1,'Dialog',11592)
Dialog.add_field(2,'HCentering',1282)
Dialog.add_field(3,'VCentering',1282)
Dialog.add_field(4,'Width',1282)
Dialog.add_field(5,'Height',1282)
Dialog.add_field(6,'Attributes',4356)
Dialog.add_field(7,'Title',8064)
Dialog.add_field(8,'Control_First',3378)
Dialog.add_field(9,'Control_Default',7474)
Dialog.add_field(10,'Control_Cancel',7474)

ControlCondition = Table('ControlCondition')
ControlCondition.add_field(1,'Dialog_',11592)
ControlCondition.add_field(2,'Control_',11570)
ControlCondition.add_field(3,'Action',11570)
ControlCondition.add_field(4,'Condition',11775)

ControlEvent = Table('ControlEvent')
ControlEvent.add_field(1,'Dialog_',11592)
ControlEvent.add_field(2,'Control_',11570)
ControlEvent.add_field(3,'Event',11570)
ControlEvent.add_field(4,'Argument',11775)
ControlEvent.add_field(5,'Condition',15871)
ControlEvent.add_field(6,'Ordering',5378)

CreateFolder = Table('CreateFolder')
CreateFolder.add_field(1,'Directory_',11592)
CreateFolder.add_field(2,'Component_',11592)

CustomAction = Table('CustomAction')
CustomAction.add_field(1,'Action',11592)
CustomAction.add_field(2,'Type',1282)
CustomAction.add_field(3,'Source',7496)
CustomAction.add_field(4,'Target',7679)

DrLocator = Table('DrLocator')
DrLocator.add_field(1,'Signature_',11592)
DrLocator.add_field(2,'Parent',15688)
DrLocator.add_field(3,'Path',15871)
DrLocator.add_field(4,'Depth',5378)

DuplicateFile = Table('DuplicateFile')
DuplicateFile.add_field(1,'FileKey',11592)
DuplicateFile.add_field(2,'Component_',3400)
DuplicateFile.add_field(3,'File_',3400)
DuplicateFile.add_field(4,'DestName',8191)
DuplicateFile.add_field(5,'DestFolder',7496)

Environment = Table('Environment')
Environment.add_field(1,'Environment',11592)
Environment.add_field(2,'Name',4095)
Environment.add_field(3,'Value',8191)
Environment.add_field(4,'Component_',3400)

Error = Table('Error')
Error.add_field(1,'Error',9474)
Error.add_field(2,'Message',7936)

EventMapping = Table('EventMapping')
EventMapping.add_field(1,'Dialog_',11592)
EventMapping.add_field(2,'Control_',11570)
EventMapping.add_field(3,'Event',11570)
EventMapping.add_field(4,'Attribute',3378)

Extension = Table('Extension')
Extension.add_field(1,'Extension',11775)
Extension.add_field(2,'Component_',11592)
Extension.add_field(3,'ProgId_',7679)
Extension.add_field(4,'MIME_',7488)
Extension.add_field(5,'Feature_',3366)

MIME = Table('MIME')
MIME.add_field(1,'ContentType',11584)
MIME.add_field(2,'Extension_',3583)
MIME.add_field(3,'CLSID',7462)

FeatureComponents = Table('FeatureComponents')
FeatureComponents.add_field(1,'Feature_',11558)
FeatureComponents.add_field(2,'Component_',11592)

FileSFPCatalog = Table('FileSFPCatalog')
FileSFPCatalog.add_field(1,'File_',11592)
FileSFPCatalog.add_field(2,'SFPCatalog_',11775)

SFPCatalog = Table('SFPCatalog')
SFPCatalog.add_field(1,'SFPCatalog',11775)
SFPCatalog.add_field(2,'Catalog',2304)
SFPCatalog.add_field(3,'Dependency',7424)

Font = Table('Font')
Font.add_field(1,'File_',11592)
Font.add_field(2,'FontTitle',7552)

IniFile = Table('IniFile')
IniFile.add_field(1,'IniFile',11592)
IniFile.add_field(2,'FileName',4095)
IniFile.add_field(3,'DirProperty',7496)
IniFile.add_field(4,'Section',3936)
IniFile.add_field(5,'Key',3968)
IniFile.add_field(6,'Value',4095)
IniFile.add_field(7,'Action',1282)
IniFile.add_field(8,'Component_',3400)

IniLocator = Table('IniLocator')
IniLocator.add_field(1,'Signature_',11592)
IniLocator.add_field(2,'FileName',3583)
IniLocator.add_field(3,'Section',3424)
IniLocator.add_field(4,'Key',3456)
IniLocator.add_field(5,'Field',5378)
IniLocator.add_field(6,'Type',5378)

InstallExecuteSequence = Table('InstallExecuteSequence')
InstallExecuteSequence.add_field(1,'Action',11592)
InstallExecuteSequence.add_field(2,'Condition',7679)
InstallExecuteSequence.add_field(3,'Sequence',5378)

InstallUISequence = Table('InstallUISequence')
InstallUISequence.add_field(1,'Action',11592)
InstallUISequence.add_field(2,'Condition',7679)
InstallUISequence.add_field(3,'Sequence',5378)

IsolatedComponent = Table('IsolatedComponent')
IsolatedComponent.add_field(1,'Component_Shared',11592)
IsolatedComponent.add_field(2,'Component_Application',11592)

LaunchCondition = Table('LaunchCondition')
LaunchCondition.add_field(1,'Condition',11775)
LaunchCondition.add_field(2,'Description',4095)

ListBox = Table('ListBox')
ListBox.add_field(1,'Property',11592)
ListBox.add_field(2,'Order',9474)
ListBox.add_field(3,'Value',3392)
ListBox.add_field(4,'Text',8000)

ListView = Table('ListView')
ListView.add_field(1,'Property',11592)
ListView.add_field(2,'Order',9474)
ListView.add_field(3,'Value',3392)
ListView.add_field(4,'Text',8000)
ListView.add_field(5,'Binary_',7496)

LockPermissions = Table('LockPermissions')
LockPermissions.add_field(1,'LockObject',11592)
LockPermissions.add_field(2,'Table',11552)
LockPermissions.add_field(3,'Domain',15871)
LockPermissions.add_field(4,'User',11775)
LockPermissions.add_field(5,'Permission',4356)

Media = Table('Media')
Media.add_field(1,'DiskId',9474)
Media.add_field(2,'LastSequence',1282)
Media.add_field(3,'DiskPrompt',8000)
Media.add_field(4,'Cabinet',7679)
Media.add_field(5,'VolumeLabel',7456)
Media.add_field(6,'Source',7496)

MoveFile = Table('MoveFile')
MoveFile.add_field(1,'FileKey',11592)
MoveFile.add_field(2,'Component_',3400)
MoveFile.add_field(3,'SourceName',8191)
MoveFile.add_field(4,'DestName',8191)
MoveFile.add_field(5,'SourceFolder',7496)
MoveFile.add_field(6,'DestFolder',3400)
MoveFile.add_field(7,'Options',1282)

MsiAssembly = Table('MsiAssembly')
MsiAssembly.add_field(1,'Component_',11592)
MsiAssembly.add_field(2,'Feature_',3366)
MsiAssembly.add_field(3,'File_Manifest',7496)
MsiAssembly.add_field(4,'File_Application',7496)
MsiAssembly.add_field(5,'Attributes',5378)

MsiAssemblyName = Table('MsiAssemblyName')
MsiAssemblyName.add_field(1,'Component_',11592)
MsiAssemblyName.add_field(2,'Name',11775)
MsiAssemblyName.add_field(3,'Value',3583)

MsiDigitalCertificate = Table('MsiDigitalCertificate')
MsiDigitalCertificate.add_field(1,'DigitalCertificate',11592)
MsiDigitalCertificate.add_field(2,'CertData',2304)

MsiDigitalSignature = Table('MsiDigitalSignature')
MsiDigitalSignature.add_field(1,'Table',11552)
MsiDigitalSignature.add_field(2,'SignObject',11592)
MsiDigitalSignature.add_field(3,'DigitalCertificate_',3400)
MsiDigitalSignature.add_field(4,'Hash',6400)

MsiFileHash = Table('MsiFileHash')
MsiFileHash.add_field(1,'File_',11592)
MsiFileHash.add_field(2,'Options',1282)
MsiFileHash.add_field(3,'HashPart1',260)
MsiFileHash.add_field(4,'HashPart2',260)
MsiFileHash.add_field(5,'HashPart3',260)
MsiFileHash.add_field(6,'HashPart4',260)

MsiPatchHeaders = Table('MsiPatchHeaders')
MsiPatchHeaders.add_field(1,'StreamRef',11558)
MsiPatchHeaders.add_field(2,'Header',2304)

ODBCAttribute = Table('ODBCAttribute')
ODBCAttribute.add_field(1,'Driver_',11592)
ODBCAttribute.add_field(2,'Attribute',11560)
ODBCAttribute.add_field(3,'Value',8191)

ODBCDriver = Table('ODBCDriver')
ODBCDriver.add_field(1,'Driver',11592)
ODBCDriver.add_field(2,'Component_',3400)
ODBCDriver.add_field(3,'Description',3583)
ODBCDriver.add_field(4,'File_',3400)
ODBCDriver.add_field(5,'File_Setup',7496)

ODBCDataSource = Table('ODBCDataSource')
ODBCDataSource.add_field(1,'DataSource',11592)
ODBCDataSource.add_field(2,'Component_',3400)
ODBCDataSource.add_field(3,'Description',3583)
ODBCDataSource.add_field(4,'DriverDescription',3583)
ODBCDataSource.add_field(5,'Registration',1282)

ODBCSourceAttribute = Table('ODBCSourceAttribute')
ODBCSourceAttribute.add_field(1,'DataSource_',11592)
ODBCSourceAttribute.add_field(2,'Attribute',11552)
ODBCSourceAttribute.add_field(3,'Value',8191)

ODBCTranslator = Table('ODBCTranslator')
ODBCTranslator.add_field(1,'Translator',11592)
ODBCTranslator.add_field(2,'Component_',3400)
ODBCTranslator.add_field(3,'Description',3583)
ODBCTranslator.add_field(4,'File_',3400)
ODBCTranslator.add_field(5,'File_Setup',7496)

Patch = Table('Patch')
Patch.add_field(1,'File_',11592)
Patch.add_field(2,'Sequence',9474)
Patch.add_field(3,'PatchSize',260)
Patch.add_field(4,'Attributes',1282)
Patch.add_field(5,'Header',6400)
Patch.add_field(6,'StreamRef_',7462)

PatchPackage = Table('PatchPackage')
PatchPackage.add_field(1,'PatchId',11558)
PatchPackage.add_field(2,'Media_',1282)

PublishComponent = Table('PublishComponent')
PublishComponent.add_field(1,'ComponentId',11558)
PublishComponent.add_field(2,'Qualifier',11775)
PublishComponent.add_field(3,'Component_',11592)
PublishComponent.add_field(4,'AppData',8191)
PublishComponent.add_field(5,'Feature_',3366)

RadioButton = Table('RadioButton')
RadioButton.add_field(1,'Property',11592)
RadioButton.add_field(2,'Order',9474)
RadioButton.add_field(3,'Value',3392)
RadioButton.add_field(4,'X',1282)
RadioButton.add_field(5,'Y',1282)
RadioButton.add_field(6,'Width',1282)
RadioButton.add_field(7,'Height',1282)
RadioButton.add_field(8,'Text',8000)
RadioButton.add_field(9,'Help',7986)

Registry = Table('Registry')
Registry.add_field(1,'Registry',11592)
Registry.add_field(2,'Root',1282)
Registry.add_field(3,'Key',4095)
Registry.add_field(4,'Name',8191)
Registry.add_field(5,'Value',7936)
Registry.add_field(6,'Component_',3400)

RegLocator = Table('RegLocator')
RegLocator.add_field(1,'Signature_',11592)
RegLocator.add_field(2,'Root',1282)
RegLocator.add_field(3,'Key',3583)
RegLocator.add_field(4,'Name',7679)
RegLocator.add_field(5,'Type',5378)

RemoveFile = Table('RemoveFile')
RemoveFile.add_field(1,'FileKey',11592)
RemoveFile.add_field(2,'Component_',3400)
RemoveFile.add_field(3,'FileName',8191)
RemoveFile.add_field(4,'DirProperty',3400)
RemoveFile.add_field(5,'InstallMode',1282)

RemoveIniFile = Table('RemoveIniFile')
RemoveIniFile.add_field(1,'RemoveIniFile',11592)
RemoveIniFile.add_field(2,'FileName',4095)
RemoveIniFile.add_field(3,'DirProperty',7496)
RemoveIniFile.add_field(4,'Section',3936)
RemoveIniFile.add_field(5,'Key',3968)
RemoveIniFile.add_field(6,'Value',8191)
RemoveIniFile.add_field(7,'Action',1282)
RemoveIniFile.add_field(8,'Component_',3400)

RemoveRegistry = Table('RemoveRegistry')
RemoveRegistry.add_field(1,'RemoveRegistry',11592)
RemoveRegistry.add_field(2,'Root',1282)
RemoveRegistry.add_field(3,'Key',4095)
RemoveRegistry.add_field(4,'Name',8191)
RemoveRegistry.add_field(5,'Component_',3400)

ReserveCost = Table('ReserveCost')
ReserveCost.add_field(1,'ReserveKey',11592)
ReserveCost.add_field(2,'Component_',3400)
ReserveCost.add_field(3,'ReserveFolder',7496)
ReserveCost.add_field(4,'ReserveLocal',260)
ReserveCost.add_field(5,'ReserveSource',260)

SelfReg = Table('SelfReg')
SelfReg.add_field(1,'File_',11592)
SelfReg.add_field(2,'Cost',5378)

ServiceControl = Table('ServiceControl')
ServiceControl.add_field(1,'ServiceControl',11592)
ServiceControl.add_field(2,'Name',4095)
ServiceControl.add_field(3,'Event',1282)
ServiceControl.add_field(4,'Arguments',8191)
ServiceControl.add_field(5,'Wait',5378)
ServiceControl.add_field(6,'Component_',3400)

ServiceInstall = Table('ServiceInstall')
ServiceInstall.add_field(1,'ServiceInstall',11592)
ServiceInstall.add_field(2,'Name',3583)
ServiceInstall.add_field(3,'DisplayName',8191)
ServiceInstall.add_field(4,'ServiceType',260)
ServiceInstall.add_field(5,'StartType',260)
ServiceInstall.add_field(6,'ErrorControl',260)
ServiceInstall.add_field(7,'LoadOrderGroup',7679)
ServiceInstall.add_field(8,'Dependencies',7679)
ServiceInstall.add_field(9,'StartName',7679)
ServiceInstall.add_field(10,'Password',7679)
ServiceInstall.add_field(11,'Arguments',7679)
ServiceInstall.add_field(12,'Component_',3400)
ServiceInstall.add_field(13,'Description',8191)

Shortcut = Table('Shortcut')
Shortcut.add_field(1,'Shortcut',11592)
Shortcut.add_field(2,'Directory_',3400)
Shortcut.add_field(3,'Name',3968)
Shortcut.add_field(4,'Component_',3400)
Shortcut.add_field(5,'Target',3400)
Shortcut.add_field(6,'Arguments',7679)
Shortcut.add_field(7,'Description',8191)
Shortcut.add_field(8,'Hotkey',5378)
Shortcut.add_field(9,'Icon_',7496)
Shortcut.add_field(10,'IconIndex',5378)
Shortcut.add_field(11,'ShowCmd',5378)
Shortcut.add_field(12,'WkDir',7496)

Signature = Table('Signature')
Signature.add_field(1,'Signature',11592)
Signature.add_field(2,'FileName',3583)
Signature.add_field(3,'MinVersion',7444)
Signature.add_field(4,'MaxVersion',7444)
Signature.add_field(5,'MinSize',4356)
Signature.add_field(6,'MaxSize',4356)
Signature.add_field(7,'MinDate',4356)
Signature.add_field(8,'MaxDate',4356)
Signature.add_field(9,'Languages',7679)

TextStyle = Table('TextStyle')
TextStyle.add_field(1,'TextStyle',11592)
TextStyle.add_field(2,'FaceName',3360)
TextStyle.add_field(3,'Size',1282)
TextStyle.add_field(4,'Color',4356)
TextStyle.add_field(5,'StyleBits',5378)

TypeLib = Table('TypeLib')
TypeLib.add_field(1,'LibID',11558)
TypeLib.add_field(2,'Language',9474)
TypeLib.add_field(3,'Component_',11592)
TypeLib.add_field(4,'Version',4356)
TypeLib.add_field(5,'Description',8064)
TypeLib.add_field(6,'Directory_',7496)
TypeLib.add_field(7,'Feature_',3366)
TypeLib.add_field(8,'Cost',4356)

UIText = Table('UIText')
UIText.add_field(1,'Key',11592)
UIText.add_field(2,'Text',8191)

Upgrade = Table('Upgrade')
Upgrade.add_field(1,'UpgradeCode',11558)
Upgrade.add_field(2,'VersionMin',15636)
Upgrade.add_field(3,'VersionMax',15636)
Upgrade.add_field(4,'Language',15871)
Upgrade.add_field(5,'Attributes',8452)
Upgrade.add_field(6,'Remove',7679)
Upgrade.add_field(7,'ActionProperty',3400)

Verb = Table('Verb')
Verb.add_field(1,'Extension_',11775)
Verb.add_field(2,'Verb',11552)
Verb.add_field(3,'Sequence',5378)
Verb.add_field(4,'Command',8191)
Verb.add_field(5,'Argument',8191)

tables=[_Validation, ActionText, AdminExecuteSequence, Condition, AdminUISequence, AdvtExecuteSequence, AdvtUISequence, AppId, AppSearch, Property, BBControl, Billboard, Feature, Binary, BindImage, File, CCPSearch, CheckBox, Class, Component, Icon, ProgId, ComboBox, CompLocator, Complus, Directory, Control, Dialog, ControlCondition, ControlEvent, CreateFolder, CustomAction, DrLocator, DuplicateFile, Environment, Error, EventMapping, Extension, MIME, FeatureComponents, FileSFPCatalog, SFPCatalog, Font, IniFile, IniLocator, InstallExecuteSequence, InstallUISequence, IsolatedComponent, LaunchCondition, ListBox, ListView, LockPermissions, Media, MoveFile, MsiAssembly, MsiAssemblyName, MsiDigitalCertificate, MsiDigitalSignature, MsiFileHash, MsiPatchHeaders, ODBCAttribute, ODBCDriver, ODBCDataSource, ODBCSourceAttribute, ODBCTranslator, Patch, PatchPackage, PublishComponent, RadioButton, Registry, RegLocator, RemoveFile, RemoveIniFile, RemoveRegistry, ReserveCost, SelfReg, ServiceControl, ServiceInstall, Shortcut, Signature, TextStyle, TypeLib, UIText, Upgrade, Verb]

_Validation_records = [
(u'_Validation',u'Table',u'N',None, None, None, None, u'Identifier',None, u'Name of table',),
(u'_Validation',u'Column',u'N',None, None, None, None, u'Identifier',None, u'Name of column',),
(u'_Validation',u'Description',u'Y',None, None, None, None, u'Text',None, u'Description of column',),
(u'_Validation',u'Set',u'Y',None, None, None, None, u'Text',None, u'Set of values that are permitted',),
(u'_Validation',u'Category',u'Y',None, None, None, None, None, u'Text;Formatted;Template;Condition;Guid;Path;Version;Language;Identifier;Binary;UpperCase;LowerCase;Filename;Paths;AnyPath;WildCardFilename;RegPath;KeyFormatted;CustomSource;Property;Cabinet;Shortcut;URL',u'String category',),
(u'_Validation',u'KeyColumn',u'Y',1,32,None, None, None, None, u'Column to which foreign key connects',),
(u'_Validation',u'KeyTable',u'Y',None, None, None, None, u'Identifier',None, u'For foreign key, Name of table to which data must link',),
(u'_Validation',u'MaxValue',u'Y',-2147483647,2147483647,None, None, None, None, u'Maximum value allowed',),
(u'_Validation',u'MinValue',u'Y',-2147483647,2147483647,None, None, None, None, u'Minimum value allowed',),
(u'_Validation',u'Nullable',u'N',None, None, None, None, None, u'Y;N;@',u'Whether the column is nullable',),
(u'ActionText',u'Description',u'Y',None, None, None, None, u'Text',None, u'Localized description displayed in progress dialog and log when action is executing.',),
(u'ActionText',u'Action',u'N',None, None, None, None, u'Identifier',None, u'Name of action to be described.',),
(u'ActionText',u'Template',u'Y',None, None, None, None, u'Template',None, u'Optional localized format template used to format action data records for display during action execution.',),
(u'AdminExecuteSequence',u'Action',u'N',None, None, None, None, u'Identifier',None, u'Name of action to invoke, either in the engine or the handler DLL.',),
(u'AdminExecuteSequence',u'Condition',u'Y',None, None, None, None, u'Condition',None, u'Optional expression which skips the action if evaluates to expFalse.If the expression syntax is invalid, the engine will terminate, returning iesBadActionData.',),
(u'AdminExecuteSequence',u'Sequence',u'Y',-4,32767,None, None, None, None, u'Number that determines the sort order in which the actions are to be executed.  Leave blank to suppress action.',),
(u'Condition',u'Condition',u'Y',None, None, None, None, u'Condition',None, u'Expression evaluated to determine if Level in the Feature table is to change.',),
(u'Condition',u'Feature_',u'N',None, None, u'Feature',1,u'Identifier',None, u'Reference to a Feature entry in Feature table.',),
(u'Condition',u'Level',u'N',0,32767,None, None, None, None, u'New selection Level to set in Feature table if Condition evaluates to TRUE.',),
(u'AdminUISequence',u'Action',u'N',None, None, None, None, u'Identifier',None, u'Name of action to invoke, either in the engine or the handler DLL.',),
(u'AdminUISequence',u'Condition',u'Y',None, None, None, None, u'Condition',None, u'Optional expression which skips the action if evaluates to expFalse.If the expression syntax is invalid, the engine will terminate, returning iesBadActionData.',),
(u'AdminUISequence',u'Sequence',u'Y',-4,32767,None, None, None, None, u'Number that determines the sort order in which the actions are to be executed.  Leave blank to suppress action.',),
(u'AdvtExecuteSequence',u'Action',u'N',None, None, None, None, u'Identifier',None, u'Name of action to invoke, either in the engine or the handler DLL.',),
(u'AdvtExecuteSequence',u'Condition',u'Y',None, None, None, None, u'Condition',None, u'Optional expression which skips the action if evaluates to expFalse.If the expression syntax is invalid, the engine will terminate, returning iesBadActionData.',),
(u'AdvtExecuteSequence',u'Sequence',u'Y',-4,32767,None, None, None, None, u'Number that determines the sort order in which the actions are to be executed.  Leave blank to suppress action.',),
(u'AdvtUISequence',u'Action',u'N',None, None, None, None, u'Identifier',None, u'Name of action to invoke, either in the engine or the handler DLL.',),
(u'AdvtUISequence',u'Condition',u'Y',None, None, None, None, u'Condition',None, u'Optional expression which skips the action if evaluates to expFalse.If the expression syntax is invalid, the engine will terminate, returning iesBadActionData.',),
(u'AdvtUISequence',u'Sequence',u'Y',-4,32767,None, None, None, None, u'Number that determines the sort order in which the actions are to be executed.  Leave blank to suppress action.',),
(u'AppId',u'AppId',u'N',None, None, None, None, u'Guid',None, None, ),
(u'AppId',u'ActivateAtStorage',u'Y',0,1,None, None, None, None, None, ),
(u'AppId',u'DllSurrogate',u'Y',None, None, None, None, u'Text',None, None, ),
(u'AppId',u'LocalService',u'Y',None, None, None, None, u'Text',None, None, ),
(u'AppId',u'RemoteServerName',u'Y',None, None, None, None, u'Formatted',None, None, ),
(u'AppId',u'RunAsInteractiveUser',u'Y',0,1,None, None, None, None, None, ),
(u'AppId',u'ServiceParameters',u'Y',None, None, None, None, u'Text',None, None, ),
(u'AppSearch',u'Property',u'N',None, None, None, None, u'Identifier',None, u'The property associated with a Signature',),
(u'AppSearch',u'Signature_',u'N',None, None, u'Signature;RegLocator;IniLocator;DrLocator;CompLocator',1,u'Identifier',None, u'The Signature_ represents a unique file signature and is also the foreign key in the Signature,  RegLocator, IniLocator, CompLocator and the DrLocator tables.',),
(u'Property',u'Property',u'N',None, None, None, None, u'Identifier',None, u'Name of property, uppercase if settable by launcher or loader.',),
(u'Property',u'Value',u'N',None, None, None, None, u'Text',None, u'String value for property.  Never null or empty.',),
(u'BBControl',u'Type',u'N',None, None, None, None, u'Identifier',None, u'The type of the control.',),
(u'BBControl',u'Y',u'N',0,32767,None, None, None, None, u'Vertical coordinate of the upper left corner of the bounding rectangle of the control.',),
(u'BBControl',u'Text',u'Y',None, None, None, None, u'Text',None, u'A string used to set the initial text contained within a control (if appropriate).',),
(u'BBControl',u'BBControl',u'N',None, None, None, None, u'Identifier',None, u'Name of the control. This name must be unique within a billboard, but can repeat on different billboard.',),
(u'BBControl',u'Attributes',u'Y',0,2147483647,None, None, None, None, u'A 32-bit word that specifies the attribute flags to be applied to this control.',),
(u'BBControl',u'Billboard_',u'N',None, None, u'Billboard',1,u'Identifier',None, u'External key to the Billboard table, name of the billboard.',),
(u'BBControl',u'Height',u'N',0,32767,None, None, None, None, u'Height of the bounding rectangle of the control.',),
(u'BBControl',u'Width',u'N',0,32767,None, None, None, None, u'Width of the bounding rectangle of the control.',),
(u'BBControl',u'X',u'N',0,32767,None, None, None, None, u'Horizontal coordinate of the upper left corner of the bounding rectangle of the control.',),
(u'Billboard',u'Action',u'Y',None, None, None, None, u'Identifier',None, u'The name of an action. The billboard is displayed during the progress messages received from this action.',),
(u'Billboard',u'Billboard',u'N',None, None, None, None, u'Identifier',None, u'Name of the billboard.',),
(u'Billboard',u'Feature_',u'N',None, None, u'Feature',1,u'Identifier',None, u'An external key to the Feature Table. The billboard is shown only if this feature is being installed.',),
(u'Billboard',u'Ordering',u'Y',0,32767,None, None, None, None, u'A positive integer. If there is more than one billboard corresponding to an action they will be shown in the order defined by this column.',),
(u'Feature',u'Description',u'Y',None, None, None, None, u'Text',None, u'Longer descriptive text describing a visible feature item.',),
(u'Feature',u'Attributes',u'N',None, None, None, None, None, u'0;1;2;4;5;6;8;9;10;16;17;18;20;21;22;24;25;26;32;33;34;36;37;38;48;49;50;52;53;54',u'Feature attributes',),
(u'Feature',u'Feature',u'N',None, None, None, None, u'Identifier',None, u'Primary key used to identify a particular feature record.',),
(u'Feature',u'Directory_',u'Y',None, None, u'Directory',1,u'UpperCase',None, u'The name of the Directory that can be configured by the UI. A non-null value will enable the browse button.',),
(u'Feature',u'Level',u'N',0,32767,None, None, None, None, u'The install level at which record will be initially selected. An install level of 0 will disable an item and prevent its display.',),
(u'Feature',u'Title',u'Y',None, None, None, None, u'Text',None, u'Short text identifying a visible feature item.',),
(u'Feature',u'Display',u'Y',0,32767,None, None, None, None, u'Numeric sort order, used to force a specific display ordering.',),
(u'Feature',u'Feature_Parent',u'Y',None, None, u'Feature',1,u'Identifier',None, u'Optional key of a parent record in the same table. If the parent is not selected, then the record will not be installed. Null indicates a root item.',),
(u'Binary',u'Name',u'N',None, None, None, None, u'Identifier',None, u'Unique key identifying the binary data.',),
(u'Binary',u'Data',u'N',None, None, None, None, u'Binary',None, u'The unformatted binary data.',),
(u'BindImage',u'File_',u'N',None, None, u'File',1,u'Identifier',None, u'The index into the File table. This must be an executable file.',),
(u'BindImage',u'Path',u'Y',None, None, None, None, u'Paths',None, u'A list of ;  delimited paths that represent the paths to be searched for the import DLLS. The list is usually a list of properties each enclosed within square brackets [] .',),
(u'File',u'Sequence',u'N',1,32767,None, None, None, None, u'Sequence with respect to the media images; order must track cabinet order.',),
(u'File',u'Attributes',u'Y',0,32767,None, None, None, None, u'Integer containing bit flags representing file attributes (with the decimal value of each bit position in parentheses)',),
(u'File',u'File',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized token, must match identifier in cabinet.  For uncompressed files, this field is ignored.',),
(u'File',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key referencing Component that controls the file.',),
(u'File',u'FileName',u'N',None, None, None, None, u'Filename',None, u'File name used for installation, may be localized.  This may contain a "short name|long name" pair.',),
(u'File',u'FileSize',u'N',0,2147483647,None, None, None, None, u'Size of file in bytes (long integer).',),
(u'File',u'Language',u'Y',None, None, None, None, u'Language',None, u'List of decimal language Ids, comma-separated if more than one.',),
(u'File',u'Version',u'Y',None, None, u'File',1,u'Version',None, u'Version string for versioned files;  Blank for unversioned files.',),
(u'CCPSearch',u'Signature_',u'N',None, None, u'Signature;RegLocator;IniLocator;DrLocator;CompLocator',1,u'Identifier',None, u'The Signature_ represents a unique file signature and is also the foreign key in the Signature,  RegLocator, IniLocator, CompLocator and the DrLocator tables.',),
(u'CheckBox',u'Property',u'N',None, None, None, None, u'Identifier',None, u'A named property to be tied to the item.',),
(u'CheckBox',u'Value',u'Y',None, None, None, None, u'Formatted',None, u'The value string associated with the item.',),
(u'Class',u'Description',u'Y',None, None, None, None, u'Text',None, u'Localized description for the Class.',),
(u'Class',u'Attributes',u'Y',None, 32767,None, None, None, None, u'Class registration attributes.',),
(u'Class',u'Feature_',u'N',None, None, u'Feature',1,u'Identifier',None, u'Required foreign key into the Feature Table, specifying the feature to validate or install in order for the CLSID factory to be operational.',),
(u'Class',u'AppId_',u'Y',None, None, u'AppId',1,u'Guid',None, u'Optional AppID containing DCOM information for associated application (string GUID).',),
(u'Class',u'Argument',u'Y',None, None, None, None, u'Formatted',None, u'optional argument for LocalServers.',),
(u'Class',u'CLSID',u'N',None, None, None, None, u'Guid',None, u'The CLSID of an OLE factory.',),
(u'Class',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Required foreign key into the Component Table, specifying the component for which to return a path when called through LocateComponent.',),
(u'Class',u'Context',u'N',None, None, None, None, u'Identifier',None, u'The numeric server context for this server. CLSCTX_xxxx',),
(u'Class',u'DefInprocHandler',u'Y',None, None, None, None, u'Filename',u'1;2;3',u'Optional default inproc handler.  Only optionally provided if Context=CLSCTX_LOCAL_SERVER.  Typically "ole32.dll" or "mapi32.dll"',),
(u'Class',u'FileTypeMask',u'Y',None, None, None, None, u'Text',None, u'Optional string containing information for the HKCRthis CLSID) key. If multiple patterns exist, they must be delimited by a semicolon, and numeric subkeys will be generated: 0,1,2...',),
(u'Class',u'Icon_',u'Y',None, None, u'Icon',1,u'Identifier',None, u'Optional foreign key into the Icon Table, specifying the icon file associated with this CLSID. Will be written under the DefaultIcon key.',),
(u'Class',u'IconIndex',u'Y',-32767,32767,None, None, None, None, u'Optional icon index.',),
(u'Class',u'ProgId_Default',u'Y',None, None, u'ProgId',1,u'Text',None, u'Optional ProgId associated with this CLSID.',),
(u'Component',u'Condition',u'Y',None, None, None, None, u'Condition',None, u"A conditional statement that will disable this component if the specified condition evaluates to the 'True' state. If a component is disabled, it will not be installed, regardless of the 'Action' state associated with the component.",),
(u'Component',u'Attributes',u'N',None, None, None, None, None, None, u'Remote execution option, one of irsEnum',),
(u'Component',u'Component',u'N',None, None, None, None, u'Identifier',None, u'Primary key used to identify a particular component record.',),
(u'Component',u'ComponentId',u'Y',None, None, None, None, u'Guid',None, u'A string GUID unique to this component, version, and language.',),
(u'Component',u'Directory_',u'N',None, None, u'Directory',1,u'Identifier',None, u'Required key of a Directory table record. This is actually a property name whose value contains the actual path, set either by the AppSearch action or with the default setting obtained from the Directory table.',),
(u'Component',u'KeyPath',u'Y',None, None, u'File;Registry;ODBCDataSource',1,u'Identifier',None, u'Either the primary key into the File table, Registry table, or ODBCDataSource table. This extract path is stored when the component is installed, and is used to detect the presence of the component and to return the path to it.',),
(u'Icon',u'Name',u'N',None, None, None, None, u'Identifier',None, u'Primary key. Name of the icon file.',),
(u'Icon',u'Data',u'N',None, None, None, None, u'Binary',None, u'Binary stream. The binary icon data in PE (.DLL or .EXE) or icon (.ICO) format.',),
(u'ProgId',u'Description',u'Y',None, None, None, None, u'Text',None, u'Localized description for the Program identifier.',),
(u'ProgId',u'Icon_',u'Y',None, None, u'Icon',1,u'Identifier',None, u'Optional foreign key into the Icon Table, specifying the icon file associated with this ProgId. Will be written under the DefaultIcon key.',),
(u'ProgId',u'IconIndex',u'Y',-32767,32767,None, None, None, None, u'Optional icon index.',),
(u'ProgId',u'ProgId',u'N',None, None, None, None, u'Text',None, u'The Program Identifier. Primary key.',),
(u'ProgId',u'Class_',u'Y',None, None, u'Class',1,u'Guid',None, u'The CLSID of an OLE factory corresponding to the ProgId.',),
(u'ProgId',u'ProgId_Parent',u'Y',None, None, u'ProgId',1,u'Text',None, u'The Parent Program Identifier. If specified, the ProgId column becomes a version independent prog id.',),
(u'ComboBox',u'Text',u'Y',None, None, None, None, u'Formatted',None, u'The visible text to be assigned to the item. Optional. If this entry or the entire column is missing, the text is the same as the value.',),
(u'ComboBox',u'Property',u'N',None, None, None, None, u'Identifier',None, u'A named property to be tied to this item. All the items tied to the same property become part of the same combobox.',),
(u'ComboBox',u'Value',u'N',None, None, None, None, u'Formatted',None, u'The value string associated with this item. Selecting the line will set the associated property to this value.',),
(u'ComboBox',u'Order',u'N',1,32767,None, None, None, None, u'A positive integer used to determine the ordering of the items within one list.\tThe integers do not have to be consecutive.',),
(u'CompLocator',u'Type',u'Y',0,1,None, None, None, None, u'A boolean value that determines if the registry value is a filename or a directory location.',),
(u'CompLocator',u'Signature_',u'N',None, None, None, None, u'Identifier',None, u'The table key. The Signature_ represents a unique file signature and is also the foreign key in the Signature table.',),
(u'CompLocator',u'ComponentId',u'N',None, None, None, None, u'Guid',None, u'A string GUID unique to this component, version, and language.',),
(u'Complus',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key referencing Component that controls the ComPlus component.',),
(u'Complus',u'ExpType',u'Y',0,32767,None, None, None, None, u'ComPlus component attributes.',),
(u'Directory',u'Directory',u'N',None, None, None, None, u'Identifier',None, u'Unique identifier for directory entry, primary key. If a property by this name is defined, it contains the full path to the directory.',),
(u'Directory',u'DefaultDir',u'N',None, None, None, None, u'DefaultDir',None, u"The default sub-path under parent's path.",),
(u'Directory',u'Directory_Parent',u'Y',None, None, u'Directory',1,u'Identifier',None, u'Reference to the entry in this table specifying the default parent directory. A record parented to itself or with a Null parent represents a root of the install tree.',),
(u'Control',u'Type',u'N',None, None, None, None, u'Identifier',None, u'The type of the control.',),
(u'Control',u'Y',u'N',0,32767,None, None, None, None, u'Vertical coordinate of the upper left corner of the bounding rectangle of the control.',),
(u'Control',u'Text',u'Y',None, None, None, None, u'Formatted',None, u'A string used to set the initial text contained within a control (if appropriate).',),
(u'Control',u'Property',u'Y',None, None, None, None, u'Identifier',None, u'The name of a defined property to be linked to this control. ',),
(u'Control',u'Attributes',u'Y',0,2147483647,None, None, None, None, u'A 32-bit word that specifies the attribute flags to be applied to this control.',),
(u'Control',u'Height',u'N',0,32767,None, None, None, None, u'Height of the bounding rectangle of the control.',),
(u'Control',u'Width',u'N',0,32767,None, None, None, None, u'Width of the bounding rectangle of the control.',),
(u'Control',u'X',u'N',0,32767,None, None, None, None, u'Horizontal coordinate of the upper left corner of the bounding rectangle of the control.',),
(u'Control',u'Control',u'N',None, None, None, None, u'Identifier',None, u'Name of the control. This name must be unique within a dialog, but can repeat on different dialogs. ',),
(u'Control',u'Control_Next',u'Y',None, None, u'Control',2,u'Identifier',None, u'The name of an other control on the same dialog. This link defines the tab order of the controls. The links have to form one or more cycles!',),
(u'Control',u'Dialog_',u'N',None, None, u'Dialog',1,u'Identifier',None, u'External key to the Dialog table, name of the dialog.',),
(u'Control',u'Help',u'Y',None, None, None, None, u'Text',None, u'The help strings used with the button. The text is optional. ',),
(u'Dialog',u'Attributes',u'Y',0,2147483647,None, None, None, None, u'A 32-bit word that specifies the attribute flags to be applied to this dialog.',),
(u'Dialog',u'Height',u'N',0,32767,None, None, None, None, u'Height of the bounding rectangle of the dialog.',),
(u'Dialog',u'Width',u'N',0,32767,None, None, None, None, u'Width of the bounding rectangle of the dialog.',),
(u'Dialog',u'Dialog',u'N',None, None, None, None, u'Identifier',None, u'Name of the dialog.',),
(u'Dialog',u'Control_Cancel',u'Y',None, None, u'Control',2,u'Identifier',None, u'Defines the cancel control. Hitting escape or clicking on the close icon on the dialog is equivalent to pushing this button.',),
(u'Dialog',u'Control_Default',u'Y',None, None, u'Control',2,u'Identifier',None, u'Defines the default control. Hitting return is equivalent to pushing this button.',),
(u'Dialog',u'Control_First',u'N',None, None, u'Control',2,u'Identifier',None, u'Defines the control that has the focus when the dialog is created.',),
(u'Dialog',u'HCentering',u'N',0,100,None, None, None, None, u'Horizontal position of the dialog on a 0-100 scale. 0 means left end, 100 means right end of the screen, 50 center.',),
(u'Dialog',u'Title',u'Y',None, None, None, None, u'Formatted',None, u"A text string specifying the title to be displayed in the title bar of the dialog's window.",),
(u'Dialog',u'VCentering',u'N',0,100,None, None, None, None, u'Vertical position of the dialog on a 0-100 scale. 0 means top end, 100 means bottom end of the screen, 50 center.',),
(u'ControlCondition',u'Action',u'N',None, None, None, None, None, u'Default;Disable;Enable;Hide;Show',u'The desired action to be taken on the specified control.',),
(u'ControlCondition',u'Condition',u'N',None, None, None, None, u'Condition',None, u'A standard conditional statement that specifies under which conditions the action should be triggered.',),
(u'ControlCondition',u'Dialog_',u'N',None, None, u'Dialog',1,u'Identifier',None, u'A foreign key to the Dialog table, name of the dialog.',),
(u'ControlCondition',u'Control_',u'N',None, None, u'Control',2,u'Identifier',None, u'A foreign key to the Control table, name of the control.',),
(u'ControlEvent',u'Condition',u'Y',None, None, None, None, u'Condition',None, u'A standard conditional statement that specifies under which conditions an event should be triggered.',),
(u'ControlEvent',u'Ordering',u'Y',0,2147483647,None, None, None, None, u'An integer used to order several events tied to the same control. Can be left blank.',),
(u'ControlEvent',u'Argument',u'N',None, None, None, None, u'Formatted',None, u'A value to be used as a modifier when triggering a particular event.',),
(u'ControlEvent',u'Dialog_',u'N',None, None, u'Dialog',1,u'Identifier',None, u'A foreign key to the Dialog table, name of the dialog.',),
(u'ControlEvent',u'Control_',u'N',None, None, u'Control',2,u'Identifier',None, u'A foreign key to the Control table, name of the control',),
(u'ControlEvent',u'Event',u'N',None, None, None, None, u'Formatted',None, u'An identifier that specifies the type of the event that should take place when the user interacts with control specified by the first two entries.',),
(u'CreateFolder',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key into the Component table.',),
(u'CreateFolder',u'Directory_',u'N',None, None, u'Directory',1,u'Identifier',None, u'Primary key, could be foreign key into the Directory table.',),
(u'CustomAction',u'Type',u'N',1,16383,None, None, None, None, u'The numeric custom action type, consisting of source location, code type, entry, option flags.',),
(u'CustomAction',u'Action',u'N',None, None, None, None, u'Identifier',None, u'Primary key, name of action, normally appears in sequence table unless private use.',),
(u'CustomAction',u'Source',u'Y',None, None, None, None, u'CustomSource',None, u'The table reference of the source of the code.',),
(u'CustomAction',u'Target',u'Y',None, None, None, None, u'Formatted',None, u'Excecution parameter, depends on the type of custom action',),
(u'DrLocator',u'Signature_',u'N',None, None, None, None, u'Identifier',None, u'The Signature_ represents a unique file signature and is also the foreign key in the Signature table.',),
(u'DrLocator',u'Path',u'Y',None, None, None, None, u'AnyPath',None, u'The path on the user system. This is a either a subpath below the value of the Parent or a full path. The path may contain properties enclosed within [ ] that will be expanded.',),
(u'DrLocator',u'Depth',u'Y',0,32767,None, None, None, None, u'The depth below the path to which the Signature_ is recursively searched. If absent, the depth is assumed to be 0.',),
(u'DrLocator',u'Parent',u'Y',None, None, None, None, u'Identifier',None, u'The parent file signature. It is also a foreign key in the Signature table. If null and the Path column does not expand to a full path, then all the fixed drives of the user system are searched using the Path.',),
(u'DuplicateFile',u'File_',u'N',None, None, u'File',1,u'Identifier',None, u'Foreign key referencing the source file to be duplicated.',),
(u'DuplicateFile',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key referencing Component that controls the duplicate file.',),
(u'DuplicateFile',u'DestFolder',u'Y',None, None, None, None, u'Identifier',None, u'Name of a property whose value is assumed to resolve to the full pathname to a destination folder.',),
(u'DuplicateFile',u'DestName',u'Y',None, None, None, None, u'Filename',None, u'Filename to be given to the duplicate file.',),
(u'DuplicateFile',u'FileKey',u'N',None, None, None, None, u'Identifier',None, u'Primary key used to identify a particular file entry',),
(u'Environment',u'Name',u'N',None, None, None, None, u'Text',None, u'The name of the environmental value.',),
(u'Environment',u'Value',u'Y',None, None, None, None, u'Formatted',None, u'The value to set in the environmental settings.',),
(u'Environment',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key into the Component table referencing component that controls the installing of the environmental value.',),
(u'Environment',u'Environment',u'N',None, None, None, None, u'Identifier',None, u'Unique identifier for the environmental variable setting',),
(u'Error',u'Error',u'N',0,32767,None, None, None, None, u'Integer error number, obtained from header file IError(...) macros.',),
(u'Error',u'Message',u'Y',None, None, None, None, u'Template',None, u'Error formatting template, obtained from user ed. or localizers.',),
(u'EventMapping',u'Dialog_',u'N',None, None, u'Dialog',1,u'Identifier',None, u'A foreign key to the Dialog table, name of the Dialog.',),
(u'EventMapping',u'Control_',u'N',None, None, u'Control',2,u'Identifier',None, u'A foreign key to the Control table, name of the control.',),
(u'EventMapping',u'Event',u'N',None, None, None, None, u'Identifier',None, u'An identifier that specifies the type of the event that the control subscribes to.',),
(u'EventMapping',u'Attribute',u'N',None, None, None, None, u'Identifier',None, u'The name of the control attribute, that is set when this event is received.',),
(u'Extension',u'Feature_',u'N',None, None, u'Feature',1,u'Identifier',None, u'Required foreign key into the Feature Table, specifying the feature to validate or install in order for the CLSID factory to be operational.',),
(u'Extension',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Required foreign key into the Component Table, specifying the component for which to return a path when called through LocateComponent.',),
(u'Extension',u'Extension',u'N',None, None, None, None, u'Text',None, u'The extension associated with the table row.',),
(u'Extension',u'MIME_',u'Y',None, None, u'MIME',1,u'Text',None, u'Optional Context identifier, typically "type/format" associated with the extension',),
(u'Extension',u'ProgId_',u'Y',None, None, u'ProgId',1,u'Text',None, u'Optional ProgId associated with this extension.',),
(u'MIME',u'CLSID',u'Y',None, None, None, None, u'Guid',None, u'Optional associated CLSID.',),
(u'MIME',u'ContentType',u'N',None, None, None, None, u'Text',None, u'Primary key. Context identifier, typically "type/format".',),
(u'MIME',u'Extension_',u'N',None, None, u'Extension',1,u'Text',None, u'Optional associated extension (without dot)',),
(u'FeatureComponents',u'Feature_',u'N',None, None, u'Feature',1,u'Identifier',None, u'Foreign key into Feature table.',),
(u'FeatureComponents',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key into Component table.',),
(u'FileSFPCatalog',u'File_',u'N',None, None, u'File',1,u'Identifier',None, u'File associated with the catalog',),
(u'FileSFPCatalog',u'SFPCatalog_',u'N',None, None, u'SFPCatalog',1,u'Filename',None, u'Catalog associated with the file',),
(u'SFPCatalog',u'SFPCatalog',u'N',None, None, None, None, u'Filename',None, u'File name for the catalog.',),
(u'SFPCatalog',u'Catalog',u'N',None, None, None, None, u'Binary',None, u'SFP Catalog',),
(u'SFPCatalog',u'Dependency',u'Y',None, None, None, None, u'Formatted',None, u'Parent catalog - only used by SFP',),
(u'Font',u'File_',u'N',None, None, u'File',1,u'Identifier',None, u'Primary key, foreign key into File table referencing font file.',),
(u'Font',u'FontTitle',u'Y',None, None, None, None, u'Text',None, u'Font name.',),
(u'IniFile',u'Action',u'N',None, None, None, None, None, u'0;1;3',u'The type of modification to be made, one of iifEnum',),
(u'IniFile',u'Value',u'N',None, None, None, None, u'Formatted',None, u'The value to be written.',),
(u'IniFile',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key into the Component table referencing component that controls the installing of the .INI value.',),
(u'IniFile',u'FileName',u'N',None, None, None, None, u'Filename',None, u'The .INI file name in which to write the information',),
(u'IniFile',u'IniFile',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized token.',),
(u'IniFile',u'DirProperty',u'Y',None, None, None, None, u'Identifier',None, u'Foreign key into the Directory table denoting the directory where the .INI file is.',),
(u'IniFile',u'Key',u'N',None, None, None, None, u'Formatted',None, u'The .INI file key below Section.',),
(u'IniFile',u'Section',u'N',None, None, None, None, u'Formatted',None, u'The .INI file Section.',),
(u'IniLocator',u'Type',u'Y',0,2,None, None, None, None, u'An integer value that determines if the .INI value read is a filename or a directory location or to be used as is w/o interpretation.',),
(u'IniLocator',u'Signature_',u'N',None, None, None, None, u'Identifier',None, u'The table key. The Signature_ represents a unique file signature and is also the foreign key in the Signature table.',),
(u'IniLocator',u'FileName',u'N',None, None, None, None, u'Filename',None, u'The .INI file name.',),
(u'IniLocator',u'Key',u'N',None, None, None, None, u'Text',None, u'Key value (followed by an equals sign in INI file).',),
(u'IniLocator',u'Section',u'N',None, None, None, None, u'Text',None, u'Section name within in file (within square brackets in INI file).',),
(u'IniLocator',u'Field',u'Y',0,32767,None, None, None, None, u'The field in the .INI line. If Field is null or 0 the entire line is read.',),
(u'InstallExecuteSequence',u'Action',u'N',None, None, None, None, u'Identifier',None, u'Name of action to invoke, either in the engine or the handler DLL.',),
(u'InstallExecuteSequence',u'Condition',u'Y',None, None, None, None, u'Condition',None, u'Optional expression which skips the action if evaluates to expFalse.If the expression syntax is invalid, the engine will terminate, returning iesBadActionData.',),
(u'InstallExecuteSequence',u'Sequence',u'Y',-4,32767,None, None, None, None, u'Number that determines the sort order in which the actions are to be executed.  Leave blank to suppress action.',),
(u'InstallUISequence',u'Action',u'N',None, None, None, None, u'Identifier',None, u'Name of action to invoke, either in the engine or the handler DLL.',),
(u'InstallUISequence',u'Condition',u'Y',None, None, None, None, u'Condition',None, u'Optional expression which skips the action if evaluates to expFalse.If the expression syntax is invalid, the engine will terminate, returning iesBadActionData.',),
(u'InstallUISequence',u'Sequence',u'Y',-4,32767,None, None, None, None, u'Number that determines the sort order in which the actions are to be executed.  Leave blank to suppress action.',),
(u'IsolatedComponent',u'Component_Application',u'N',None, None, u'Component',1,u'Identifier',None, u'Key to Component table item for application',),
(u'IsolatedComponent',u'Component_Shared',u'N',None, None, u'Component',1,u'Identifier',None, u'Key to Component table item to be isolated',),
(u'LaunchCondition',u'Description',u'N',None, None, None, None, u'Formatted',None, u'Localizable text to display when condition fails and install must abort.',),
(u'LaunchCondition',u'Condition',u'N',None, None, None, None, u'Condition',None, u'Expression which must evaluate to TRUE in order for install to commence.',),
(u'ListBox',u'Text',u'Y',None, None, None, None, u'Text',None, u'The visible text to be assigned to the item. Optional. If this entry or the entire column is missing, the text is the same as the value.',),
(u'ListBox',u'Property',u'N',None, None, None, None, u'Identifier',None, u'A named property to be tied to this item. All the items tied to the same property become part of the same listbox.',),
(u'ListBox',u'Value',u'N',None, None, None, None, u'Formatted',None, u'The value string associated with this item. Selecting the line will set the associated property to this value.',),
(u'ListBox',u'Order',u'N',1,32767,None, None, None, None, u'A positive integer used to determine the ordering of the items within one list..The integers do not have to be consecutive.',),
(u'ListView',u'Text',u'Y',None, None, None, None, u'Text',None, u'The visible text to be assigned to the item. Optional. If this entry or the entire column is missing, the text is the same as the value.',),
(u'ListView',u'Property',u'N',None, None, None, None, u'Identifier',None, u'A named property to be tied to this item. All the items tied to the same property become part of the same listview.',),
(u'ListView',u'Value',u'N',None, None, None, None, u'Identifier',None, u'The value string associated with this item. Selecting the line will set the associated property to this value.',),
(u'ListView',u'Order',u'N',1,32767,None, None, None, None, u'A positive integer used to determine the ordering of the items within one list..The integers do not have to be consecutive.',),
(u'ListView',u'Binary_',u'Y',None, None, u'Binary',1,u'Identifier',None, u'The name of the icon to be displayed with the icon. The binary information is looked up from the Binary Table.',),
(u'LockPermissions',u'Table',u'N',None, None, None, None, u'Identifier',u'Directory;File;Registry',u'Reference to another table name',),
(u'LockPermissions',u'Domain',u'Y',None, None, None, None, u'Formatted',None, u'Domain name for user whose permissions are being set. (usually a property)',),
(u'LockPermissions',u'LockObject',u'N',None, None, None, None, u'Identifier',None, u'Foreign key into Registry or File table',),
(u'LockPermissions',u'Permission',u'Y',-2147483647,2147483647,None, None, None, None, u'Permission Access mask.  Full Control = 268435456 (GENERIC_ALL = 0x10000000)',),
(u'LockPermissions',u'User',u'N',None, None, None, None, u'Formatted',None, u'User for permissions to be set.  (usually a property)',),
(u'Media',u'Source',u'Y',None, None, None, None, u'Property',None, u'The property defining the location of the cabinet file.',),
(u'Media',u'Cabinet',u'Y',None, None, None, None, u'Cabinet',None, u'If some or all of the files stored on the media are compressed in a cabinet, the name of that cabinet.',),
(u'Media',u'DiskId',u'N',1,32767,None, None, None, None, u'Primary key, integer to determine sort order for table.',),
(u'Media',u'DiskPrompt',u'Y',None, None, None, None, u'Text',None, u'Disk name: the visible text actually printed on the disk.  This will be used to prompt the user when this disk needs to be inserted.',),
(u'Media',u'LastSequence',u'N',0,32767,None, None, None, None, u'File sequence number for the last file for this media.',),
(u'Media',u'VolumeLabel',u'Y',None, None, None, None, u'Text',None, u'The label attributed to the volume.',),
(u'ModuleComponents',u'Component',u'N',None, None, u'Component',1,u'Identifier',None, u'Component contained in the module.',),
(u'ModuleComponents',u'Language',u'N',None, None, u'ModuleSignature',2,None, None, u'Default language ID for module (may be changed by transform).',),
(u'ModuleComponents',u'ModuleID',u'N',None, None, u'ModuleSignature',1,u'Identifier',None, u'Module containing the component.',),
(u'ModuleSignature',u'Language',u'N',None, None, None, None, None, None, u'Default decimal language of module.',),
(u'ModuleSignature',u'Version',u'N',None, None, None, None, u'Version',None, u'Version of the module.',),
(u'ModuleSignature',u'ModuleID',u'N',None, None, None, None, u'Identifier',None, u'Module identifier (String.GUID).',),
(u'ModuleDependency',u'ModuleID',u'N',None, None, u'ModuleSignature',1,u'Identifier',None, u'Module requiring the dependency.',),
(u'ModuleDependency',u'ModuleLanguage',u'N',None, None, u'ModuleSignature',2,None, None, u'Language of module requiring the dependency.',),
(u'ModuleDependency',u'RequiredID',u'N',None, None, None, None, None, None, u'String.GUID of required module.',),
(u'ModuleDependency',u'RequiredLanguage',u'N',None, None, None, None, None, None, u'LanguageID of the required module.',),
(u'ModuleDependency',u'RequiredVersion',u'Y',None, None, None, None, u'Version',None, u'Version of the required version.',),
(u'ModuleExclusion',u'ModuleID',u'N',None, None, u'ModuleSignature',1,u'Identifier',None, u'String.GUID of module with exclusion requirement.',),
(u'ModuleExclusion',u'ModuleLanguage',u'N',None, None, u'ModuleSignature',2,None, None, u'LanguageID of module with exclusion requirement.',),
(u'ModuleExclusion',u'ExcludedID',u'N',None, None, None, None, None, None, u'String.GUID of excluded module.',),
(u'ModuleExclusion',u'ExcludedLanguage',u'N',None, None, None, None, None, None, u'Language of excluded module.',),
(u'ModuleExclusion',u'ExcludedMaxVersion',u'Y',None, None, None, None, u'Version',None, u'Maximum version of excluded module.',),
(u'ModuleExclusion',u'ExcludedMinVersion',u'Y',None, None, None, None, u'Version',None, u'Minimum version of excluded module.',),
(u'MoveFile',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'If this component is not "selected" for installation or removal, no action will be taken on the associated MoveFile entry',),
(u'MoveFile',u'DestFolder',u'N',None, None, None, None, u'Identifier',None, u'Name of a property whose value is assumed to resolve to the full path to the destination directory',),
(u'MoveFile',u'DestName',u'Y',None, None, None, None, u'Filename',None, u'Name to be given to the original file after it is moved or copied.  If blank, the destination file will be given the same name as the source file',),
(u'MoveFile',u'FileKey',u'N',None, None, None, None, u'Identifier',None, u'Primary key that uniquely identifies a particular MoveFile record',),
(u'MoveFile',u'Options',u'N',0,1,None, None, None, None, u'Integer value specifying the MoveFile operating mode, one of imfoEnum',),
(u'MoveFile',u'SourceFolder',u'Y',None, None, None, None, u'Identifier',None, u'Name of a property whose value is assumed to resolve to the full path to the source directory',),
(u'MoveFile',u'SourceName',u'Y',None, None, None, None, u'Text',None, u"Name of the source file(s) to be moved or copied.  Can contain the '*' or '?' wildcards.",),
(u'MsiAssembly',u'Attributes',u'Y',None, None, None, None, None, None, u'Assembly attributes',),
(u'MsiAssembly',u'Feature_',u'N',None, None, u'Feature',1,u'Identifier',None, u'Foreign key into Feature table.',),
(u'MsiAssembly',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key into Component table.',),
(u'MsiAssembly',u'File_Application',u'Y',None, None, u'File',1,u'Identifier',None, u'Foreign key into File table, denoting the application context for private assemblies. Null for global assemblies.',),
(u'MsiAssembly',u'File_Manifest',u'Y',None, None, u'File',1,u'Identifier',None, u'Foreign key into the File table denoting the manifest file for the assembly.',),
(u'MsiAssemblyName',u'Name',u'N',None, None, None, None, u'Text',None, u'The name part of the name-value pairs for the assembly name.',),
(u'MsiAssemblyName',u'Value',u'N',None, None, None, None, u'Text',None, u'The value part of the name-value pairs for the assembly name.',),
(u'MsiAssemblyName',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key into Component table.',),
(u'MsiDigitalCertificate',u'CertData',u'N',None, None, None, None, u'Binary',None, u'A certificate context blob for a signer certificate',),
(u'MsiDigitalCertificate',u'DigitalCertificate',u'N',None, None, None, None, u'Identifier',None, u'A unique identifier for the row',),
(u'MsiDigitalSignature',u'Table',u'N',None, None, None, None, None, u'Media',u'Reference to another table name (only Media table is supported)',),
(u'MsiDigitalSignature',u'DigitalCertificate_',u'N',None, None, u'MsiDigitalCertificate',1,u'Identifier',None, u'Foreign key to MsiDigitalCertificate table identifying the signer certificate',),
(u'MsiDigitalSignature',u'Hash',u'Y',None, None, None, None, u'Binary',None, u'The encoded hash blob from the digital signature',),
(u'MsiDigitalSignature',u'SignObject',u'N',None, None, None, None, u'Text',None, u'Foreign key to Media table',),
(u'MsiFileHash',u'File_',u'N',None, None, u'File',1,u'Identifier',None, u'Primary key, foreign key into File table referencing file with this hash',),
(u'MsiFileHash',u'Options',u'N',0,32767,None, None, None, None, u'Various options and attributes for this hash.',),
(u'MsiFileHash',u'HashPart1',u'N',None, None, None, None, None, None, u'Size of file in bytes (long integer).',),
(u'MsiFileHash',u'HashPart2',u'N',None, None, None, None, None, None, u'Size of file in bytes (long integer).',),
(u'MsiFileHash',u'HashPart3',u'N',None, None, None, None, None, None, u'Size of file in bytes (long integer).',),
(u'MsiFileHash',u'HashPart4',u'N',None, None, None, None, None, None, u'Size of file in bytes (long integer).',),
(u'MsiPatchHeaders',u'StreamRef',u'N',None, None, None, None, u'Identifier',None, u'Primary key. A unique identifier for the row.',),
(u'MsiPatchHeaders',u'Header',u'N',None, None, None, None, u'Binary',None, u'Binary stream. The patch header, used for patch validation.',),
(u'ODBCAttribute',u'Value',u'Y',None, None, None, None, u'Text',None, u'Value for ODBC driver attribute',),
(u'ODBCAttribute',u'Attribute',u'N',None, None, None, None, u'Text',None, u'Name of ODBC driver attribute',),
(u'ODBCAttribute',u'Driver_',u'N',None, None, u'ODBCDriver',1,u'Identifier',None, u'Reference to ODBC driver in ODBCDriver table',),
(u'ODBCDriver',u'Description',u'N',None, None, None, None, u'Text',None, u'Text used as registered name for driver, non-localized',),
(u'ODBCDriver',u'File_',u'N',None, None, u'File',1,u'Identifier',None, u'Reference to key driver file',),
(u'ODBCDriver',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Reference to associated component',),
(u'ODBCDriver',u'Driver',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized.internal token for driver',),
(u'ODBCDriver',u'File_Setup',u'Y',None, None, u'File',1,u'Identifier',None, u'Optional reference to key driver setup DLL',),
(u'ODBCDataSource',u'Description',u'N',None, None, None, None, u'Text',None, u'Text used as registered name for data source',),
(u'ODBCDataSource',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Reference to associated component',),
(u'ODBCDataSource',u'DataSource',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized.internal token for data source',),
(u'ODBCDataSource',u'DriverDescription',u'N',None, None, None, None, u'Text',None, u'Reference to driver description, may be existing driver',),
(u'ODBCDataSource',u'Registration',u'N',0,1,None, None, None, None, u'Registration option: 0=machine, 1=user, others t.b.d.',),
(u'ODBCSourceAttribute',u'Value',u'Y',None, None, None, None, u'Text',None, u'Value for ODBC data source attribute',),
(u'ODBCSourceAttribute',u'Attribute',u'N',None, None, None, None, u'Text',None, u'Name of ODBC data source attribute',),
(u'ODBCSourceAttribute',u'DataSource_',u'N',None, None, u'ODBCDataSource',1,u'Identifier',None, u'Reference to ODBC data source in ODBCDataSource table',),
(u'ODBCTranslator',u'Description',u'N',None, None, None, None, u'Text',None, u'Text used as registered name for translator',),
(u'ODBCTranslator',u'File_',u'N',None, None, u'File',1,u'Identifier',None, u'Reference to key translator file',),
(u'ODBCTranslator',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Reference to associated component',),
(u'ODBCTranslator',u'File_Setup',u'Y',None, None, u'File',1,u'Identifier',None, u'Optional reference to key translator setup DLL',),
(u'ODBCTranslator',u'Translator',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized.internal token for translator',),
(u'Patch',u'Sequence',u'N',0,32767,None, None, None, None, u'Primary key, sequence with respect to the media images; order must track cabinet order.',),
(u'Patch',u'Attributes',u'N',0,32767,None, None, None, None, u'Integer containing bit flags representing patch attributes',),
(u'Patch',u'File_',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized token, foreign key to File table, must match identifier in cabinet.',),
(u'Patch',u'Header',u'Y',None, None, None, None, u'Binary',None, u'Binary stream. The patch header, used for patch validation.',),
(u'Patch',u'PatchSize',u'N',0,2147483647,None, None, None, None, u'Size of patch in bytes (long integer).',),
(u'Patch',u'StreamRef_',u'Y',None, None, None, None, u'Identifier',None, u'Identifier. Foreign key to the StreamRef column of the MsiPatchHeaders table.',),
(u'PatchPackage',u'Media_',u'N',0,32767,None, None, None, None, u'Foreign key to DiskId column of Media table. Indicates the disk containing the patch package.',),
(u'PatchPackage',u'PatchId',u'N',None, None, None, None, u'Guid',None, u'A unique string GUID representing this patch.',),
(u'PublishComponent',u'Feature_',u'N',None, None, u'Feature',1,u'Identifier',None, u'Foreign key into the Feature table.',),
(u'PublishComponent',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key into the Component table.',),
(u'PublishComponent',u'ComponentId',u'N',None, None, None, None, u'Guid',None, u'A string GUID that represents the component id that will be requested by the alien product.',),
(u'PublishComponent',u'AppData',u'Y',None, None, None, None, u'Text',None, u'This is localisable Application specific data that can be associated with a Qualified Component.',),
(u'PublishComponent',u'Qualifier',u'N',None, None, None, None, u'Text',None, u'This is defined only when the ComponentId column is an Qualified Component Id. This is the Qualifier for ProvideComponentIndirect.',),
(u'RadioButton',u'Y',u'N',0,32767,None, None, None, None, u'The vertical coordinate of the upper left corner of the bounding rectangle of the radio button.',),
(u'RadioButton',u'Text',u'Y',None, None, None, None, u'Text',None, u'The visible title to be assigned to the radio button.',),
(u'RadioButton',u'Property',u'N',None, None, None, None, u'Identifier',None, u'A named property to be tied to this radio button. All the buttons tied to the same property become part of the same group.',),
(u'RadioButton',u'Height',u'N',0,32767,None, None, None, None, u'The height of the button.',),
(u'RadioButton',u'Width',u'N',0,32767,None, None, None, None, u'The width of the button.',),
(u'RadioButton',u'X',u'N',0,32767,None, None, None, None, u'The horizontal coordinate of the upper left corner of the bounding rectangle of the radio button.',),
(u'RadioButton',u'Value',u'N',None, None, None, None, u'Formatted',None, u'The value string associated with this button. Selecting the button will set the associated property to this value.',),
(u'RadioButton',u'Order',u'N',1,32767,None, None, None, None, u'A positive integer used to determine the ordering of the items within one list..The integers do not have to be consecutive.',),
(u'RadioButton',u'Help',u'Y',None, None, None, None, u'Text',None, u'The help strings used with the button. The text is optional.',),
(u'Registry',u'Name',u'Y',None, None, None, None, u'Formatted',None, u'The registry value name.',),
(u'Registry',u'Value',u'Y',None, None, None, None, u'Formatted',None, u'The registry value.',),
(u'Registry',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key into the Component table referencing component that controls the installing of the registry value.',),
(u'Registry',u'Key',u'N',None, None, None, None, u'RegPath',None, u'The key for the registry value.',),
(u'Registry',u'Registry',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized token.',),
(u'Registry',u'Root',u'N',-1,3,None, None, None, None, u'The predefined root key for the registry value, one of rrkEnum.',),
(u'RegLocator',u'Name',u'Y',None, None, None, None, u'Formatted',None, u'The registry value name.',),
(u'RegLocator',u'Type',u'Y',0,18,None, None, None, None, u'An integer value that determines if the registry value is a filename or a directory location or to be used as is w/o interpretation.',),
(u'RegLocator',u'Signature_',u'N',None, None, None, None, u'Identifier',None, u'The table key. The Signature_ represents a unique file signature and is also the foreign key in the Signature table. If the type is 0, the registry values refers a directory, and _Signature is not a foreign key.',),
(u'RegLocator',u'Key',u'N',None, None, None, None, u'RegPath',None, u'The key for the registry value.',),
(u'RegLocator',u'Root',u'N',0,3,None, None, None, None, u'The predefined root key for the registry value, one of rrkEnum.',),
(u'RemoveFile',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key referencing Component that controls the file to be removed.',),
(u'RemoveFile',u'FileKey',u'N',None, None, None, None, u'Identifier',None, u'Primary key used to identify a particular file entry',),
(u'RemoveFile',u'FileName',u'Y',None, None, None, None, u'WildCardFilename',None, u'Name of the file to be removed.',),
(u'RemoveFile',u'DirProperty',u'N',None, None, None, None, u'Identifier',None, u'Name of a property whose value is assumed to resolve to the full pathname to the folder of the file to be removed.',),
(u'RemoveFile',u'InstallMode',u'N',None, None, None, None, None, u'1;2;3',u'Installation option, one of iimEnum.',),
(u'RemoveIniFile',u'Action',u'N',None, None, None, None, None, u'2;4',u'The type of modification to be made, one of iifEnum.',),
(u'RemoveIniFile',u'Value',u'Y',None, None, None, None, u'Formatted',None, u'The value to be deleted. The value is required when Action is iifIniRemoveTag',),
(u'RemoveIniFile',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key into the Component table referencing component that controls the deletion of the .INI value.',),
(u'RemoveIniFile',u'FileName',u'N',None, None, None, None, u'Filename',None, u'The .INI file name in which to delete the information',),
(u'RemoveIniFile',u'DirProperty',u'Y',None, None, None, None, u'Identifier',None, u'Foreign key into the Directory table denoting the directory where the .INI file is.',),
(u'RemoveIniFile',u'Key',u'N',None, None, None, None, u'Formatted',None, u'The .INI file key below Section.',),
(u'RemoveIniFile',u'Section',u'N',None, None, None, None, u'Formatted',None, u'The .INI file Section.',),
(u'RemoveIniFile',u'RemoveIniFile',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized token.',),
(u'RemoveRegistry',u'Name',u'Y',None, None, None, None, u'Formatted',None, u'The registry value name.',),
(u'RemoveRegistry',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key into the Component table referencing component that controls the deletion of the registry value.',),
(u'RemoveRegistry',u'Key',u'N',None, None, None, None, u'RegPath',None, u'The key for the registry value.',),
(u'RemoveRegistry',u'Root',u'N',-1,3,None, None, None, None, u'The predefined root key for the registry value, one of rrkEnum',),
(u'RemoveRegistry',u'RemoveRegistry',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized token.',),
(u'ReserveCost',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Reserve a specified amount of space if this component is to be installed.',),
(u'ReserveCost',u'ReserveFolder',u'Y',None, None, None, None, u'Identifier',None, u'Name of a property whose value is assumed to resolve to the full path to the destination directory',),
(u'ReserveCost',u'ReserveKey',u'N',None, None, None, None, u'Identifier',None, u'Primary key that uniquely identifies a particular ReserveCost record',),
(u'ReserveCost',u'ReserveLocal',u'N',0,2147483647,None, None, None, None, u'Disk space to reserve if linked component is installed locally.',),
(u'ReserveCost',u'ReserveSource',u'N',0,2147483647,None, None, None, None, u'Disk space to reserve if linked component is installed to run from the source location.',),
(u'SelfReg',u'File_',u'N',None, None, u'File',1,u'Identifier',None, u'Foreign key into the File table denoting the module that needs to be registered.',),
(u'SelfReg',u'Cost',u'Y',0,32767,None, None, None, None, u'The cost of registering the module.',),
(u'ServiceControl',u'Name',u'N',None, None, None, None, u'Formatted',None, u'Name of a service. /, \\, comma and space are invalid',),
(u'ServiceControl',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Required foreign key into the Component Table that controls the startup of the service',),
(u'ServiceControl',u'Event',u'N',0,187,None, None, None, None, u'Bit field:  Install:  0x1 = Start, 0x2 = Stop, 0x8 = Delete, Uninstall: 0x10 = Start, 0x20 = Stop, 0x80 = Delete',),
(u'ServiceControl',u'ServiceControl',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized token.',),
(u'ServiceControl',u'Arguments',u'Y',None, None, None, None, u'Formatted',None, u'Arguments for the service.  Separate by [~].',),
(u'ServiceControl',u'Wait',u'Y',0,1,None, None, None, None, u'Boolean for whether to wait for the service to fully start',),
(u'ServiceInstall',u'Name',u'N',None, None, None, None, u'Formatted',None, u'Internal Name of the Service',),
(u'ServiceInstall',u'Description',u'Y',None, None, None, None, u'Text',None, u'Description of service.',),
(u'ServiceInstall',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Required foreign key into the Component Table that controls the startup of the service',),
(u'ServiceInstall',u'Arguments',u'Y',None, None, None, None, u'Formatted',None, u'Arguments to include in every start of the service, passed to WinMain',),
(u'ServiceInstall',u'ServiceInstall',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized token.',),
(u'ServiceInstall',u'Dependencies',u'Y',None, None, None, None, u'Formatted',None, u'Other services this depends on to start.  Separate by [~], and end with [~][~]',),
(u'ServiceInstall',u'DisplayName',u'Y',None, None, None, None, u'Formatted',None, u'External Name of the Service',),
(u'ServiceInstall',u'ErrorControl',u'N',-2147483647,2147483647,None, None, None, None, u'Severity of error if service fails to start',),
(u'ServiceInstall',u'LoadOrderGroup',u'Y',None, None, None, None, u'Formatted',None, u'LoadOrderGroup',),
(u'ServiceInstall',u'Password',u'Y',None, None, None, None, u'Formatted',None, u'password to run service with.  (with StartName)',),
(u'ServiceInstall',u'ServiceType',u'N',-2147483647,2147483647,None, None, None, None, u'Type of the service',),
(u'ServiceInstall',u'StartName',u'Y',None, None, None, None, u'Formatted',None, u'User or object name to run service as',),
(u'ServiceInstall',u'StartType',u'N',0,4,None, None, None, None, u'Type of the service',),
(u'Shortcut',u'Name',u'N',None, None, None, None, u'Filename',None, u'The name of the shortcut to be created.',),
(u'Shortcut',u'Description',u'Y',None, None, None, None, u'Text',None, u'The description for the shortcut.',),
(u'Shortcut',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Foreign key into the Component table denoting the component whose selection gates the shortcut creation/deletion.',),
(u'Shortcut',u'Icon_',u'Y',None, None, u'Icon',1,u'Identifier',None, u'Foreign key into the File table denoting the external icon file for the shortcut.',),
(u'Shortcut',u'IconIndex',u'Y',-32767,32767,None, None, None, None, u'The icon index for the shortcut.',),
(u'Shortcut',u'Directory_',u'N',None, None, u'Directory',1,u'Identifier',None, u'Foreign key into the Directory table denoting the directory where the shortcut file is created.',),
(u'Shortcut',u'Target',u'N',None, None, None, None, u'Shortcut',None, u'The shortcut target. This is usually a property that is expanded to a file or a folder that the shortcut points to.',),
(u'Shortcut',u'Arguments',u'Y',None, None, None, None, u'Formatted',None, u'The command-line arguments for the shortcut.',),
(u'Shortcut',u'Shortcut',u'N',None, None, None, None, u'Identifier',None, u'Primary key, non-localized token.',),
(u'Shortcut',u'Hotkey',u'Y',0,32767,None, None, None, None, u'The hotkey for the shortcut. It has the virtual-key code for the key in the low-order byte, and the modifier flags in the high-order byte. ',),
(u'Shortcut',u'ShowCmd',u'Y',None, None, None, None, None, u'1;3;7',u'The show command for the application window.The following values may be used.',),
(u'Shortcut',u'WkDir',u'Y',None, None, None, None, u'Identifier',None, u'Name of property defining location of working directory.',),
(u'Signature',u'FileName',u'N',None, None, None, None, u'Filename',None, u'The name of the file. This may contain a "short name|long name" pair.',),
(u'Signature',u'Signature',u'N',None, None, None, None, u'Identifier',None, u'The table key. The Signature represents a unique file signature.',),
(u'Signature',u'Languages',u'Y',None, None, None, None, u'Language',None, u'The languages supported by the file.',),
(u'Signature',u'MaxDate',u'Y',0,2147483647,None, None, None, None, u'The maximum creation date of the file.',),
(u'Signature',u'MaxSize',u'Y',0,2147483647,None, None, None, None, u'The maximum size of the file. ',),
(u'Signature',u'MaxVersion',u'Y',None, None, None, None, u'Text',None, u'The maximum version of the file.',),
(u'Signature',u'MinDate',u'Y',0,2147483647,None, None, None, None, u'The minimum creation date of the file.',),
(u'Signature',u'MinSize',u'Y',0,2147483647,None, None, None, None, u'The minimum size of the file.',),
(u'Signature',u'MinVersion',u'Y',None, None, None, None, u'Text',None, u'The minimum version of the file.',),
(u'TextStyle',u'TextStyle',u'N',None, None, None, None, u'Identifier',None, u'Name of the style. The primary key of this table. This name is embedded in the texts to indicate a style change.',),
(u'TextStyle',u'Color',u'Y',0,16777215,None, None, None, None, u'A long integer indicating the color of the string in the RGB format (Red, Green, Blue each 0-255, RGB = R + 256*G + 256^2*B).',),
(u'TextStyle',u'FaceName',u'N',None, None, None, None, u'Text',None, u'A string indicating the name of the font used. Required. The string must be at most 31 characters long.',),
(u'TextStyle',u'Size',u'N',0,32767,None, None, None, None, u'The size of the font used. This size is given in our units (1/12 of the system font height). Assuming that the system font is set to 12 point size, this is equivalent to the point size.',),
(u'TextStyle',u'StyleBits',u'Y',0,15,None, None, None, None, u'A combination of style bits.',),
(u'TypeLib',u'Description',u'Y',None, None, None, None, u'Text',None, None, ),
(u'TypeLib',u'Feature_',u'N',None, None, u'Feature',1,u'Identifier',None, u'Required foreign key into the Feature Table, specifying the feature to validate or install in order for the type library to be operational.',),
(u'TypeLib',u'Component_',u'N',None, None, u'Component',1,u'Identifier',None, u'Required foreign key into the Component Table, specifying the component for which to return a path when called through LocateComponent.',),
(u'TypeLib',u'Directory_',u'Y',None, None, u'Directory',1,u'Identifier',None, u'Optional. The foreign key into the Directory table denoting the path to the help file for the type library.',),
(u'TypeLib',u'Language',u'N',0,32767,None, None, None, None, u'The language of the library.',),
(u'TypeLib',u'Version',u'Y',0,16777215,None, None, None, None, u'The version of the library. The minor version is in the lower 8 bits of the integer. The major version is in the next 16 bits. ',),
(u'TypeLib',u'Cost',u'Y',0,2147483647,None, None, None, None, u'The cost associated with the registration of the typelib. This column is currently optional.',),
(u'TypeLib',u'LibID',u'N',None, None, None, None, u'Guid',None, u'The GUID that represents the library.',),
(u'UIText',u'Text',u'Y',None, None, None, None, u'Text',None, u'The localized version of the string.',),
(u'UIText',u'Key',u'N',None, None, None, None, u'Identifier',None, u'A unique key that identifies the particular string.',),
(u'Upgrade',u'Attributes',u'N',0,2147483647,None, None, None, None, u'The attributes of this product set.',),
(u'Upgrade',u'Language',u'Y',None, None, None, None, u'Language',None, u'A comma-separated list of languages for either products in this set or products not in this set.',),
(u'Upgrade',u'ActionProperty',u'N',None, None, None, None, u'UpperCase',None, u'The property to set when a product in this set is found.',),
(u'Upgrade',u'Remove',u'Y',None, None, None, None, u'Formatted',None, u'The list of features to remove when uninstalling a product from this set.  The default is "ALL".',),
(u'Upgrade',u'UpgradeCode',u'N',None, None, None, None, u'Guid',None, u'The UpgradeCode GUID belonging to the products in this set.',),
(u'Upgrade',u'VersionMax',u'Y',None, None, None, None, u'Text',None, u'The maximum ProductVersion of the products in this set.  The set may or may not include products with this particular version.',),
(u'Upgrade',u'VersionMin',u'Y',None, None, None, None, u'Text',None, u'The minimum ProductVersion of the products in this set.  The set may or may not include products with this particular version.',),
(u'Verb',u'Sequence',u'Y',0,32767,None, None, None, None, u'Order within the verbs for a particular extension. Also used simply to specify the default verb.',),
(u'Verb',u'Argument',u'Y',None, None, None, None, u'Formatted',None, u'Optional value for the command arguments.',),
(u'Verb',u'Extension_',u'N',None, None, u'Extension',1,u'Text',None, u'The extension associated with the table row.',),
(u'Verb',u'Verb',u'N',None, None, None, None, u'Text',None, u'The verb for the command.',),
(u'Verb',u'Command',u'Y',None, None, None, None, u'Formatted',None, u'The command text.',),
]
