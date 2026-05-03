using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace RunXsltTransform
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: RunXsltTransform <input-xml-file>");
                return 1;
            }

            string inputFile = args[0];
            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Error: Input file '{inputFile}' not found.");
                return 1;
            }

            // Проверка, что файл не пустой
            FileInfo fi = new FileInfo(inputFile);
            if (fi.Length == 0)
            {
                Console.WriteLine("Warning: Input file is empty. Nothing to process.");
                return 0;
            }

            try
            {
                // --- ДОПОЛНИТЕЛЬНАЯ ФУНКЦИОНАЛЬНОСТЬ: добавить атрибут SumAmount в корневой элемент Pay ---
                AddSumAmountToInputFile(inputFile);

                // --- ОСНОВНАЯ ЛОГИКА: преобразование в Employees.xml ---
                // 1. Загружаем входной XML
                XDocument inputDoc = XDocument.Load(inputFile);
                var items = inputDoc.Descendants("item");
                if (!items.Any())
                {
                    Console.WriteLine("Warning: No <item> elements found. Nothing to do.");
                    return 0;
                }

                // Группируем по name+surname
                var groups = items.GroupBy(item => new { Name = (string)item.Attribute("name"), Surname = (string)item.Attribute("surname") });

                // 2. Загружаем существующий Employees.xml (если есть)
                string outputFile = "Employees.xml";
                XDocument outputDoc;
                XElement root;

                if (File.Exists(outputFile))
                {
                    outputDoc = XDocument.Load(outputFile);
                    root = outputDoc.Root;
                    if (root == null || root.Name.LocalName != "Employees")
                    {
                        Console.WriteLine("Error: Existing Employees.xml has invalid root element.");
                        return 1;
                    }
                }
                else
                {
                    outputDoc = new XDocument(new XElement("Employees"));
                    root = outputDoc.Root;
                }

                bool anyChanges = false;

                // 3. Обрабатываем каждую группу из входного файла
                foreach (var group in groups)
                {
                    string name = group.Key.Name;
                    string surname = group.Key.Surname;

                    XElement existingEmployee = root.Elements("Employee")
                        .FirstOrDefault(e => (string)e.Attribute("name") == name && (string)e.Attribute("surname") == surname);

                    if (existingEmployee == null)
                    {
                        XElement newEmployee = new XElement("Employee",
                            new XAttribute("name", name),
                            new XAttribute("surname", surname)
                        );

                        double totalSalary = 0.0;
                        foreach (var item in group)
                        {
                            string mount = GetCorrectMount(item);
                            string amountStr = (string)item.Attribute("amount");
                            double amount = ParseAmount(amountStr);
                            totalSalary += amount;

                            newEmployee.Add(new XElement("salary",
                                new XAttribute("amount", amountStr),
                                new XAttribute("mount", mount)
                            ));
                        }
                        newEmployee.Add(new XAttribute("SumSalary", totalSalary.ToString(CultureInfo.InvariantCulture)));
                        root.Add(newEmployee);
                        anyChanges = true;
                        Console.WriteLine($"Added new employee: {name} {surname}");
                    }
                    else
                    {
                        bool employeeChanged = false;
                        double currentSum = ParseAmount((string)existingEmployee.Attribute("SumSalary"));

                        foreach (var item in group)
                        {
                            string mount = GetCorrectMount(item);
                            string amountStr = (string)item.Attribute("amount");
                            double amount = ParseAmount(amountStr);

                            bool mountExists = existingEmployee.Elements("salary")
                                .Any(s => (string)s.Attribute("mount") == mount);

                            if (!mountExists)
                            {
                                existingEmployee.Add(new XElement("salary",
                                    new XAttribute("amount", amountStr),
                                    new XAttribute("mount", mount)
                                ));
                                currentSum += amount;
                                employeeChanged = true;
                                Console.WriteLine($"Added new salary for {name} {surname}, month={mount}, amount={amountStr}");
                            }
                            else
                            {
                                Console.WriteLine($"Skipped duplicate salary for {name} {surname}, month={mount} (already exists)");
                            }
                        }

                        if (employeeChanged)
                        {
                            existingEmployee.Attribute("SumSalary").Value = currentSum.ToString(CultureInfo.InvariantCulture);
                            anyChanges = true;
                        }
                    }
                }

                // 4. Сохраняем Employees.xml, если были изменения
                if (anyChanges)
                {
                    outputDoc.Save(outputFile);
                    Console.WriteLine("Employees.xml successfully updated.");
                }
                else
                {
                    Console.WriteLine("No new employees or salary entries to add.");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Добавляет (или обновляет) атрибут SumAmount в корневой элемент Pay.
        /// Значение атрибута = сумма всех amount из всех элементов item.
        /// </summary>
        static void AddSumAmountToInputFile(string filePath)
        {
            // Загружаем документ
            XDocument doc = XDocument.Load(filePath);
            XElement root = doc.Root;
            if (root == null || root.Name.LocalName != "Pay")
            {
                Console.WriteLine("Warning: Root element is not <Pay>. Skipping SumAmount addition.");
                return;
            }

            // Вычисляем сумму всех amount
            double totalSum = 0.0;
            var allItems = doc.Descendants("item");
            foreach (var item in allItems)
            {
                string amountStr = (string)item.Attribute("amount");
                totalSum += ParseAmount(amountStr);
            }

            // Добавляем или обновляем атрибут SumAmount
            XAttribute sumAttr = root.Attribute("SumAmount");
            if (sumAttr == null)
                root.Add(new XAttribute("SumAmount", totalSum.ToString(CultureInfo.InvariantCulture)));
            else
                sumAttr.Value = totalSum.ToString(CultureInfo.InvariantCulture);

            // Сохраняем файл (перезаписываем)
            doc.Save(filePath);
            Console.WriteLine($"Added/updated SumAmount = {totalSum} in {filePath}");
        }

        static string GetCorrectMount(XElement item)
        {
            XElement parent = item.Parent;
            if (parent != null && parent.Name.LocalName == "Pay")
                return (string)item.Attribute("mount");
            else
                return parent?.Name.LocalName ?? "";
        }

        static double ParseAmount(string amountStr)
        {
            if (string.IsNullOrEmpty(amountStr)) return 0.0;
            string normalized = amountStr.Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out double result) ? result : 0.0;
        }
    }
}