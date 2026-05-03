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

            string inputFile = "Data1.xml";
            string employeesFile = "Employees.xml";

            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Файл {inputFile} не найден!");
                return;
            }

            try
            {
                // 1. Добавляем атрибут SumAmount
                SalaryProcessor.AddSumAmountToInputFile(inputFile);
                Console.WriteLine("Атрибут SumAmount добавлен.");

                // 2. Обновляем Employees.xml (с проверкой дубликатов по месяцам)
                SalaryProcessor.GenerateOrUpdateEmployeesFile(inputFile, employeesFile);
                Console.WriteLine($"Файл {employeesFile} успешно обновлён.");

                Console.WriteLine("Готово.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
    }
}