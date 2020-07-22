#include "Logging.h"

#include <iostream>
#include <fstream>

#include <iomanip>
#include <chrono>

void LogToFile(std::string what)
{
    std::ofstream myfile;

    time_t now = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());

    myfile.open("C:/PowerToys.log", std::fstream::app);
    myfile << now << " " << what << "\n";
    myfile.close();
}
