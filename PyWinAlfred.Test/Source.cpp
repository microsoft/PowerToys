#include <tchar.h>
#include <stdio.h>
#include "Python.h"
#include <thread>
#include <future>

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

	// 启动子线程前执行，为了释放PyEval_InitThreads获得的全局锁，否则子线程可能无法获取到全局锁。
	PyEval_ReleaseLock(); 
	PyGILState_STATE gstate = PyGILState_Ensure();

	// Initialise the Python interpreter


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

	//PyErr_Clear();

	printf("My thread is finishing... %s \n",para);
	PyGILState_Release(gstate);
	return str_ret;
}

int main(int argc, char *argv[])
{
	char* directory = "d:\\github\\WinAlfred\\Plugins\\WinAlfred.Plugin.DouBan\\";
	char* file = "main";
	char* method = "query";
	char* para1 = "movie 1";
	char* para2 = "movie 2";
	char* para3 = "movie 3";
	char* para4 = "movie 4";
	int i  = 0;
	// 初始化
	Py_Initialize();
	// 初始化线程支持
	PyEval_InitThreads();

	//std::async(Exec,directory,file,method,para);
	std::async(Exec,directory,file,method,para1);
	std::async(Exec,directory,file,method,para2);
	std::async(Exec,directory,file,method,para3);
	std::async(Exec,directory,file,method,para4);
	// 保证子线程调用都结束后
	//PyGILState_Ensure();
	getchar();
	Py_Finalize();
	return 0;
}