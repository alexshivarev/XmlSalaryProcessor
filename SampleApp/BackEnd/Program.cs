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

            FileInfo fi = new FileInfo(inputFile);
            if (fi.Length == 0)
            {
                Console.WriteLine("Warning: Input file is empty. Nothing to process.");
                return 0;
            }

            try
            {
                // Добавляем атрибут SumAmount в корневой элемент Pay
                AddSumAmountToInputFile(inputFile);

                // Основная обработка
                XDocument inputDoc = XDocument.Load(inputFile);
                var items = inputDoc.Descendants("item");
                if (!items.Any())
                {
                    Console.WriteLine("Warning: No <item> elements found. Nothing to do.");
                    return 0;
                }

                var groups = items.GroupBy(item => new { Name = (string)item.Attribute("name"), Surname = (string)item.Attribute("surname") });

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

                        decimal totalSalary = 0m;
                        foreach (var item in group)
                        {
                            string mount = GetCorrectMount(item);
                            string amountStr = (string)item.Attribute("amount");
                            decimal amount = ParseAmountDecimal(amountStr);
                            totalSalary += amount;

                            newEmployee.Add(new XElement("salary",
                                new XAttribute("amount", amountStr),
                                new XAttribute("mount", mount)
                            ));
                        }
                        decimal roundedTotal = Math.Round(totalSalary, 2, MidpointRounding.AwayFromZero);
                        newEmployee.Add(new XAttribute("SumSalary", roundedTotal.ToString(CultureInfo.InvariantCulture)));
                        root.Add(newEmployee);
                        anyChanges = true;
                        Console.WriteLine($"Added new employee: {name} {surname}");
                    }
                    else
                    {
                        bool employeeChanged = false;
                        decimal currentSum = ParseAmountDecimal((string)existingEmployee.Attribute("SumSalary"));

                        foreach (var item in group)
                        {
                            string mount = GetCorrectMount(item);
                            string amountStr = (string)item.Attribute("amount");
                            decimal amount = ParseAmountDecimal(amountStr);

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
                            decimal rounded = Math.Round(currentSum, 2, MidpointRounding.AwayFromZero);
                            existingEmployee.Attribute("SumSalary").Value = rounded.ToString(CultureInfo.InvariantCulture);
                            anyChanges = true;
                        }
                    }
                }

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

        static void AddSumAmountToInputFile(string filePath)
        {
            XDocument doc = XDocument.Load(filePath);
            XElement root = doc.Root;
            if (root == null || root.Name.LocalName != "Pay")
            {
                Console.WriteLine("Warning: Root element is not <Pay>. Skipping SumAmount addition.");
                return;
            }

            decimal totalSum = 0m;
            var allItems = doc.Descendants("item");
            foreach (var item in allItems)
            {
                string amountStr = (string)item.Attribute("amount");
                totalSum += ParseAmountDecimal(amountStr);
            }

            decimal roundedTotal = Math.Round(totalSum, 2, MidpointRounding.AwayFromZero);
            string sumValue = roundedTotal.ToString(CultureInfo.InvariantCulture);

            XAttribute sumAttr = root.Attribute("SumAmount");
            if (sumAttr == null)
                root.Add(new XAttribute("SumAmount", sumValue));
            else
                sumAttr.Value = sumValue;

            doc.Save(filePath);
            Console.WriteLine($"Added/updated SumAmount = {sumValue} in {filePath}");
        }

        static string GetCorrectMount(XElement item)
        {
            XElement parent = item.Parent;
            if (parent != null && parent.Name.LocalName == "Pay")
                return (string)item.Attribute("mount");
            else
                return parent?.Name.LocalName ?? "";
        }

        static decimal ParseAmountDecimal(string amountStr)
        {
            if (string.IsNullOrEmpty(amountStr)) return 0m;
            string normalized = amountStr.Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                return result;
            return 0m;
        }
    }
}