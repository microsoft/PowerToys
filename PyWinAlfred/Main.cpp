#include <tchar.h>
#include "Python.h"
#include <thread>
#include <future>

int i = 0;

extern "C" __declspec(dllexport) void InitPythonEnv()
{
	Py_Initialize();
	PyEval_InitThreads();
	PyEval_ReleaseLock();
	// 启动子线程前执行，为了释放PyEval_InitThreads获得的全局锁，否则子线程可能无法获取到全局锁。
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

	PyGILState_STATE gstate = PyGILState_Ensure();

	// Build the name object
	PyObject *path = PySys_GetObject("path");
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

	//PyErr_Clear();
	PyGILState_Release(gstate);

	return str_ret;
}

extern "C" __declspec(dllexport) char* ExecPython(char* directory, char* file, char* method, char* para)
{
	char* s = Exec(directory,file,method,para);
	PyGILState_Ensure();
	return s;
	//auto future = std::async(Exec,directory,file,method,para);
	//return future.get();
}