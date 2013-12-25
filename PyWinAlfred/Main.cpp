#include <tchar.h>
#include "Python.h"


extern "C" __declspec(dllexport) void InitPythonEnv()
{

}

extern "C" __declspec(dllexport) char* ExecPython(char* directory, char* file, char* query)
{
	try{
		PyObject *pName, *pModule, *pDict, *pFunc, *pValue, *pClass, *pInstance;

		// Initialise the Python interpreter
		Py_Initialize();

		// Create GIL/enable threads
		PyEval_InitThreads();

		PyGILState_STATE gstate = PyGILState_Ensure();
		//      // Get the default thread state  
		//      PyThreadState* state = PyThreadState_Get();
		//      // Once in each thread
		//PyThreadState* stateForNewThread = PyThreadState_New(state->interp);
		//PyEval_RestoreThread(stateForNewThread);

		// Build the name object
		PyObject *sys = PyImport_ImportModule("sys");
		PyObject *path = PyObject_GetAttrString(sys, "path");
		PyList_Append(path, PyString_FromString(directory));

		pName = PyString_FromString(file);

		// Load the module object
		pModule = PyImport_Import(pName);

		// pDict is a borrowed reference 
		pDict = PyModule_GetDict(pModule);

		// pFunc is also a borrowed reference 
		pClass = PyDict_GetItemString(pDict,"PyWinAlfred");

		if (PyCallable_Check(pClass)) 
		{
			pInstance = PyObject_CallObject(pClass, NULL);
		} 
		else 
		{
			PyErr_Print();
			return "failed";
		}

		// Call a method of the class with two parameters
		pValue = PyObject_CallMethod(pInstance,"query", "(s)",query);
		char * str_ret = PyString_AsString(pValue); 

		PyGILState_Release(gstate);
		//PyEval_SaveThread();

		// Finish the Python Interpreter
		//Py_Finalize();

		return str_ret;
	}
	catch(int& value){
		PyErr_Print();
	}
}