#include <tchar.h>
#include "Python.h"

extern "C" __declspec(dllexport) void ExecPython(char* directory, char* file, char* query)
{
	try{
		PyObject *pName, *pModule, *pDict, *pFunc, *pValue, *pClass, *pInstance ;

		// Initialize the Python Interpreter
		Py_Initialize();

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
			return;
		}

		// Call a method of the class with two parameters
		pValue = PyObject_CallMethod(pInstance,"query", "(s)",query);

		// Finish the Python Interpreter
		Py_Finalize();
	}
	catch(int& value){
		PyErr_Print();
	}
}