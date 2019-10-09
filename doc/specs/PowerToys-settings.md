# Power Toys Settings Framework and Core Infrastructure
The Power Toys app will have a settings framework that each Power Toy can plug into.  The settings framework has a UI frame that creates a page for each Power Toy.  The UI frame should use the Navigation View “hamburger” UI.  Each Power Toy will represent its settings as a json blob as described below.  

Each Power Toy will live in a separate .dll and be run in a separate thread by the main Power Toys process.  The main Power Toys .exe will expose key global Windows event handlers so that there is only one system-level hook for these critical events.  The current set of Power Toys require these global events.  This list will be amended as new Power Toys are authored that require additional global hooks.
* SetWinEventHook - FancyZones requires knowledge of when a window enters the move/size loop. It listens for EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZEEND, and EVENT_OBJECT_LOCATIONCHANGE messages from SetWinEventHook.
* Low-level keyboard hook - The Windows key Shortcut Guide and FancyZones both require low-level keybord hooks to intercept keyboard input and get a first chance to process it.  Other Power Toys will require this as well

* Each Power Toy must listen for 4 events:
    * Enable – When invoked, enables the Power Toys’ functionality and performs any necessary initialization.  Invoked with a JSON string from the persisted settings store
    * Disable – When invoked, disables the Power Toys’ functionality and performs any clean-up to suspend all resource use
    * OutputSettings – Return a json serialized blob of the settings for the Power Toy
    * InputSettings – Invoked with a JSON string with updated settings from the UI which is then deserialized and the state is applied.  If the settings cannot be applied by the Power Toy, the PT must return an error and an error string for the end user
* Each Power Toy may optionally provide one or more custom configuration UIs that can be invoked from its settings page
    * Each custom UI is specified as a JSON string in the settings property bag
    * The Power Toy must provide a named method that returns a serialized JSON settings string for the settings framework to call
    * The method should launch UI to edit the settings but the UI shown must be asynchronous and not block the setting UI
* The Power Toys main .exe will provide a method called InvokeSettingsUI that will show the settings dialog for the calling Power Toy.   
* Settings will be serialized by the settings framework and will be read at launch of the Power Toys framework and each Power Toy’s settings will be passed into the PT’s Enable method
* Settings will be serialized on a per-user basis
* The Settings JSON format will be versioned and each payload must specify it's version attribute.  The initial version is 1.0
 
## Power Toys Settings Object
The settings JSON object for each Power Toy should provide:
* Title string
* Icon
* Logo Image
* Credits string
* Credits link
* Settings property bag.  Each item in the property bag has two items:
   * String: display name
   * String: property / editor type
* Version number: Currently only 1.0 is supported

Property Bag of settings in priority order (type->editor)
* Bool->slide switch
* Int->free text box
* String->free text box
* Int ->Up/Down spinner
* Color-> Color picker
* Image->File picker, preview area, drag and drop
* Cursor->file picker and drop down, possibly an image
* Property Bag JSON string->Button to launch a custom editor from the Power Toy
* Method name to invoke.  The method will return a serialized JSON string with the updated custom editor settings
* String to display on the button
* Percentage->Slider
* Time->Time picker
* Date->Date picker
* IP address->masked text box
 
## PowerToys Main Settings Page
* Need to get Nick to help with the settings UI design (see attached for a whiteboard sketch)
* Need to have a settings page for overall PowerToys which will include the following
    * Check for updates
    * Startup at launch
    * Enable / disable for each utility.  
        * This invokes the Enable and Disable events for the PowerToy and suspends all resource use including CPU, GPU, Networking, Disk I/O and memory commit
* The settings UI should have an “Apply” button which will push the settings object to 
