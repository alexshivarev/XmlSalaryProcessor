using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace XmlSalaryProcessor.Core
{
    public static class SalaryProcessor
    {
        /// <summary>
        /// Парсит строку с суммой, нормализуя запятую как десятичный разделитель.
        /// </summary>
        public static decimal ParseAmountDecimal(string? amountStr)
        {
            if (string.IsNullOrWhiteSpace(amountStr))
                return 0;
            // Замена запятой на точку для инвариантного парсинга
            string normalized = amountStr.Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                return result;
            decimal.TryParse(amountStr, out result);
            return result;
        }

        /// <summary>
        /// Добавляет атрибут SumAmount в корневой элемент входного XML-файла.
        /// </summary>
        public static void AddSumAmountToInputFile(string inputFilePath)
        {
            var doc = XDocument.Load(inputFilePath);
            var root = doc.Root;
            if (root == null) throw new InvalidOperationException("XML документ не имеет корневого элемента.");
            decimal sum = root.Descendants("item")
                .Sum(item => ParseAmountDecimal(item.Attribute("amount")?.Value));
            root.SetAttributeValue("SumAmount", sum.ToString(CultureInfo.InvariantCulture));
            doc.Save(inputFilePath);
        }

        /// <summary>
        /// Перегрузка для работы с XDocument (веб-версия).
        /// </summary>
        public static void AddSumAmountToXDocument(XDocument doc)
        {
            var root = doc.Root;
            if (root == null) return;
            decimal sum = root.Descendants("item")
                .Sum(item => ParseAmountDecimal(item.Attribute("amount")?.Value));
            root.SetAttributeValue("SumAmount", sum.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Обновляет или создаёт Employees.xml в соответствии с требуемым форматом.
        /// </summary>
        public static void GenerateOrUpdateEmployeesFile(string sourceXmlPath, string employeesXmlPath)
        {
            var sourceDoc = XDocument.Load(sourceXmlPath);
            var items = sourceDoc.Descendants("item").ToList();
            if (!items.Any()) return;

            // Группировка по name + surname
            var grouped = items
                .GroupBy(item => new
                {
                    Name = item.Attribute("name")?.Value ?? "",
                    Surname = item.Attribute("surname")?.Value ?? ""
                })
                .Where(g => !string.IsNullOrWhiteSpace(g.Key.Name) || !string.IsNullOrWhiteSpace(g.Key.Surname))
                .Select(g => new { g.Key.Name, g.Key.Surname, Items = g.ToList() })
                .ToList();

            XDocument employeesDoc;
            if (File.Exists(employeesXmlPath))
                employeesDoc = XDocument.Load(employeesXmlPath);
            else
                employeesDoc = new XDocument(new XElement("Employees"));

            var root = employeesDoc.Root;
            if (root == null)
            {
                root = new XElement("Employees");
                employeesDoc.Add(root);
            }

            foreach (var empGroup in grouped)
            {
                string name = empGroup.Name;
                string surname = empGroup.Surname;
                var empElement = root.Elements("Employee")
                    .FirstOrDefault(e => (string?)e.Attribute("name") == name && (string?)e.Attribute("surname") == surname);
                if (empElement == null)
                {
                    empElement = new XElement("Employee",
                        new XAttribute("name", name),
                        new XAttribute("surname", surname));
                    root.Add(empElement);
                }

                // Добавляем новые salary (без дублей по mount)
                foreach (var item in empGroup.Items)
                {
                    string? mount = item.Attribute("mount")?.Value;
                    if (string.IsNullOrEmpty(mount)) continue;
                    string? amountStr = item.Attribute("amount")?.Value;
                    if (amountStr == null) continue;

                    // Проверка существования записи за этот mount у данного сотрудника
                    var existingSalary = empElement.Elements("salary")
                        .FirstOrDefault(s => (string?)s.Attribute("mount") == mount);
                    if (existingSalary != null) continue; // пропускаем дубликат

                    empElement.Add(new XElement("salary",
                        new XAttribute("amount", amountStr), // оригинальная строка
                        new XAttribute("mount", mount)));
                }

                // Пересчёт SumSalary
                decimal total = 0m;
                foreach (var salary in empElement.Elements("salary"))
                {
                    string? amountAttr = salary.Attribute("amount")?.Value;
                    total += ParseAmountDecimal(amountAttr);
                }
                empElement.SetAttributeValue("SumSalary", total.ToString(CultureInfo.InvariantCulture));
            }

            employeesDoc.Save(employeesXmlPath);
        }

        /// <summary>
        /// Генерирует XDocument для Employees.xml (веб-версия).
        /// </summary>
        public static XDocument GenerateEmployeesDocument(XDocument sourceDoc)
        {
            var items = sourceDoc.Descendants("item").ToList();
            if (!items.Any()) return new XDocument(new XElement("Employees"));

            var grouped = items
                .GroupBy(item => new
                {
                    Name = item.Attribute("name")?.Value ?? "",
                    Surname = item.Attribute("surname")?.Value ?? ""
                })
                .Where(g => !string.IsNullOrWhiteSpace(g.Key.Name) || !string.IsNullOrWhiteSpace(g.Key.Surname))
                .Select(g => new { g.Key.Name, g.Key.Surname, Items = g.ToList() })
                .ToList();

            var employeesDoc = new XDocument(new XElement("Employees"));
            var root = employeesDoc.Root!;

            foreach (var empGroup in grouped)
            {
                var empElement = new XElement("Employee",
                    new XAttribute("name", empGroup.Name),
                    new XAttribute("surname", empGroup.Surname));

                var processedMounts = new HashSet<string>();
                foreach (var item in empGroup.Items)
                {
                    string? mount = item.Attribute("mount")?.Value;
                    if (string.IsNullOrEmpty(mount)) continue;
                    if (processedMounts.Contains(mount)) continue;
                    processedMounts.Add(mount);

                    string? amountStr = item.Attribute("amount")?.Value;
                    if (amountStr == null) continue;

                    empElement.Add(new XElement("salary",
                        new XAttribute("amount", amountStr),
                        new XAttribute("mount", mount)));
                }

                // Подсчёт суммы
                decimal total = 0m;
                foreach (var salary in empElement.Elements("salary"))
                {
                    string? amountAttr = salary.Attribute("amount")?.Value;
                    total += ParseAmountDecimal(amountAttr);
                }
                empElement.SetAttributeValue("SumSalary", total.ToString(CultureInfo.InvariantCulture));

                root.Add(empElement);
            }

            return employeesDoc;
        }
    }
}