using System;
using System.IO;
using XmlSalaryProcessor.Core;

namespace BackEnd
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Salary Processor (Console)");
            Console.WriteLine("==========================");

            string inputFile = GetInputFilePath(args);
            if (string.IsNullOrEmpty(inputFile) || !File.Exists(inputFile))
            {
                Console.WriteLine($"Файл не найден: {inputFile ?? "не указан"}");
                Console.WriteLine("Укажите путь к XML-файлу, например:");
                Console.WriteLine("  dotnet run -- Data1_typical.xml");
                Console.WriteLine("  dotnet run -- ../BackEndTests/TestData/Data1_typical.xml");
                return;
            }

            Console.WriteLine($"Обрабатывается файл: {inputFile}");

            void LogWarning(string msg) => Console.WriteLine($"ПРЕДУПРЕЖДЕНИЕ: {msg}");

            try
            {
                SalaryProcessor.AddSumAmountToInputFile(inputFile, LogWarning);
                Console.WriteLine("Атрибут SumAmount добавлен/обновлён.");

                string? directory = Path.GetDirectoryName(inputFile);
                string employeesFile = Path.Combine(directory ?? ".", "Employees.xml");
                SalaryProcessor.GenerateOrUpdateEmployeesFile(inputFile, employeesFile, LogWarning);
                Console.WriteLine($"Файл {employeesFile} создан/обновлён.");

                Console.WriteLine("Готово.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
            }
        }

        private static string GetInputFilePath(string[] args)
        {
            string currentDir = Directory.GetCurrentDirectory();
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Аргумент командной строки
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                string userPath = args[0];
                // Абсолютный путь
                if (File.Exists(userPath))
                    return Path.GetFullPath(userPath);
                // Относительно текущей рабочей папки
                string relCurrent = Path.Combine(currentDir, userPath);
                if (File.Exists(relCurrent))
                    return Path.GetFullPath(relCurrent);
                // Относительно папки с EXE
                string relExe = Path.Combine(exeDir, userPath);
                if (File.Exists(relExe))
                    return Path.GetFullPath(relExe);
                // Не нашли – вернём последний вариант (для сообщения об ошибке)
                return relCurrent;
            }

            // 2. Без аргументов – ищем стандартные имена в стандартных местах
            string[] defaultNames = { "Data1_typical.xml", "Data1.xml", "input.xml" };
            string[] searchDirs = {
                currentDir,
                exeDir,
                Path.Combine(exeDir, "TestData"),
                Path.GetFullPath(Path.Combine(exeDir, @"..\..\..\BackEndTests\TestData")),
                Path.GetFullPath(Path.Combine(exeDir, @"..\..\..\..\BackEndTests\TestData"))
            };

            foreach (var dir in searchDirs)
            {
                foreach (var name in defaultNames)
                {
                    string full = Path.Combine(dir, name);
                    if (File.Exists(full))
                        return full;
                }
            }

            return Path.Combine(searchDirs[0], defaultNames[0]);
        }
    }
}