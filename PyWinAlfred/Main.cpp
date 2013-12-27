#include <tchar.h>
#include "Python.h"
#include <thread>
#include <future>

extern "C" __declspec(dllexport) void InitPythonEnv()
{
	Py_Initialize();
	PyEval_InitThreads();
}

char* GetErrorMessage()
{
	char *pStrErrorMessage = NULL;

	if(PyErr_Occurred()){
		PyObject *ptype, *pvalue, *ptraceback;
		PyErr_Fetch(&ptype, &pvalue, &ptraceback);
		pStrErrorMessage = PyString_AsString(pvalue);
	}

	return pStrErrorMessage;
}


char* Exec(char* directory, char* file, char* method, char* para)
{
	PyObject *pName, *pModule, *pDict, *pFunc, *pValue, *pClass, *pInstance;
	char *error;

	PyThreadState* global_state = PyThreadState_Get();
	PyThreadState* ts = Py_NewInterpreter();
	PyThreadState_Swap(ts);
	// Initialise the Python interpreter

	// Create GIL/enable threads

	//PyGILState_STATE gstate = PyGILState_Ensure();
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
	error = GetErrorMessage();
	if(error != NULL){
		char* err =new char[5000]();
		sprintf(error, "%s:%s","PYTHONERROR",error);
		return err;
	}

	pModule = PyImport_Import(pName);
	error = GetErrorMessage();
	if(error != NULL){
		char* err =new char[5000]();
		sprintf(err, "%s:%s","PYTHONERROR",error);
		return err;
	}

	pDict = PyModule_GetDict(pModule);
	error = GetErrorMessage();
	if(error != NULL){
		char* err =new char[5000]();
		sprintf(err, "%s:%s","PYTHONERROR",error);
		return err;
	}

	pClass = PyDict_GetItemString(pDict,"PyWinAlfred");
	error = GetErrorMessage();
	if(error != NULL){
		char* err =new char[5000]();
		sprintf(err, "%s:%s","PYTHONERROR",error);
		return err;
	}

	pInstance = PyObject_CallObject(pClass, NULL);
	error = GetErrorMessage();
	if(error != NULL){
		char* err =new char[5000]();
		sprintf(err, "%s:%s","PYTHONERROR",error);
		return err;
	}

	// Call a method of the class with two parameters
	pValue = PyObject_CallMethod(pInstance,method, "(s)",para);
	error = GetErrorMessage();
	if(error != NULL){
		char* err =new char[5000]();
		sprintf(err, "%s:%s","PYTHONERROR",error);
		return err;
	}

	char * str_ret = PyString_AsString(pValue); 

	//PyEval_SaveThread();

	// Finish the Python Interpreter

	PyErr_Clear();
	Py_EndInterpreter(ts);
	PyThreadState_Swap(global_state);

	return str_ret;
}

extern "C" __declspec(dllexport) char* ExecPython(char* directory, char* file, char* method, char* para)
{
	auto future = std::async(Exec,directory,file,method,para);
	return future.get();
}