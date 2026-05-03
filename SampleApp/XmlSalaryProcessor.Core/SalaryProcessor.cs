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
        public static decimal ParseAmountDecimal(string? amountStr)
        {
            if (string.IsNullOrWhiteSpace(amountStr))
                return 0;
            string normalized = amountStr.Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                return result;
            decimal.TryParse(amountStr, out result);
            return result;
        }

        // --- AddSumAmountToInputFile (консоль) ---
        public static void AddSumAmountToInputFile(string inputFilePath, Action<string>? onWarning = null)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(inputFilePath);
            }
            catch (Exception ex)
            {
                onWarning?.Invoke($"Ошибка загрузки XML: {ex.Message}");
                throw;
            }
            var root = doc.Root;
            if (root == null)
            {
                onWarning?.Invoke("Файл не содержит корневого элемента.");
                return;
            }
            decimal sum = 0;
            int itemCount = 0;
            foreach (var item in root.Descendants("item"))
            {
                itemCount++;
                string? amountStr = item.Attribute("amount")?.Value;
                decimal val = ParseAmountDecimal(amountStr);
                if (val == 0 && !string.IsNullOrWhiteSpace(amountStr) && amountStr != "0")
                    onWarning?.Invoke($"Предупреждение в строке {itemCount}: значение amount '{amountStr}' не распознано, считается как 0.");
                sum += val;
            }
            root.SetAttributeValue("SumAmount", sum.ToString(CultureInfo.InvariantCulture));
            doc.Save(inputFilePath);
        }

        public static void AddSumAmountToXDocument(XDocument doc, Action<string>? onWarning = null)
        {
            var root = doc.Root;
            if (root == null) return;
            decimal sum = 0;
            int idx = 0;
            foreach (var item in root.Descendants("item"))
            {
                idx++;
                string? amountStr = item.Attribute("amount")?.Value;
                decimal val = ParseAmountDecimal(amountStr);
                if (val == 0 && !string.IsNullOrWhiteSpace(amountStr) && amountStr != "0")
                    onWarning?.Invoke($"Предупреждение в строке {idx}: значение amount '{amountStr}' не распознано, считается как 0.");
                sum += val;
            }
            root.SetAttributeValue("SumAmount", sum.ToString(CultureInfo.InvariantCulture));
        }

        // --- GenerateOrUpdateEmployeesFile (консоль) ---
        public static void GenerateOrUpdateEmployeesFile(string sourceXmlPath, string employeesXmlPath, Action<string>? onWarning = null)
        {
            XDocument sourceDoc;
            try
            {
                sourceDoc = XDocument.Load(sourceXmlPath);
            }
            catch (Exception ex)
            {
                onWarning?.Invoke($"Ошибка загрузки XML: {ex.Message}");
                throw;
            }

            var items = sourceDoc.Descendants("item").ToList();
            if (!items.Any())
            {
                onWarning?.Invoke("В файле нет элементов <item>.");
                // Всё равно создаём пустой Employees.xml
                var emptyDoc = new XDocument(new XElement("Employees"));
                emptyDoc.Save(employeesXmlPath);
                return;
            }

            // Группировка с сохранением номеров строк
            var grouped = new Dictionary<string, List<(XElement Item, int LineNumber)>>();
            int lineNumber = 0;
            foreach (var item in items)
            {
                lineNumber++;
                string name = item.Attribute("name")?.Value ?? "";
                string surname = item.Attribute("surname")?.Value ?? "";
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(surname))
                {
                    onWarning?.Invoke($"Строка {lineNumber}: пропущена (отсутствуют name и surname).");
                    continue;
                }
                string key = $"{name}|{surname}";
                if (!grouped.ContainsKey(key))
                    grouped[key] = new List<(XElement, int)>();
                grouped[key].Add((item, lineNumber));
            }

            // Загрузка существующего Employees.xml или создание нового
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

            foreach (var kvp in grouped)
            {
                string fullName = kvp.Key;
                string[] parts = fullName.Split('|');
                string name = parts[0];
                string surname = parts[1];
                var empElement = root.Elements("Employee")
                    .FirstOrDefault(e => (string?)e.Attribute("name") == name && (string?)e.Attribute("surname") == surname);
                if (empElement == null)
                {
                    empElement = new XElement("Employee",
                        new XAttribute("name", name),
                        new XAttribute("surname", surname));
                    root.Add(empElement);
                }

                // Для отслеживания уже добавленных месяцев и их строк
                var existingMonths = empElement.Elements("salary")
                    .ToDictionary(s => (string?)s.Attribute("mount") ?? "", s => s);

                foreach (var (item, line) in kvp.Value)
                {
                    string? mount = item.Attribute("mount")?.Value;
                    if (string.IsNullOrEmpty(mount))
                    {
                        onWarning?.Invoke($"Строка {line}: пропущена (отсутствует атрибут mount).");
                        continue;
                    }
                    string? amountStr = item.Attribute("amount")?.Value;
                    if (string.IsNullOrEmpty(amountStr))
                    {
                        onWarning?.Invoke($"Строка {line}: пропущена (отсутствует атрибут amount).");
                        continue;
                    }

                    if (existingMonths.ContainsKey(mount))
                    {
                        // Дубликат: находим номер строки из существующей записи (если сохранили)
                        // Для простоты выведем сообщение, но номер строки дубликата мы уже знаем (line)
                        onWarning?.Invoke($"Строка {line}: дубликат месяца '{mount}' для сотрудника {name} {surname}. Запись пропущена.");
                        continue;
                    }

                    // Добавляем новую запись
                    var salaryElem = new XElement("salary",
                        new XAttribute("amount", amountStr),
                        new XAttribute("mount", mount));
                    empElement.Add(salaryElem);
                    existingMonths[mount] = salaryElem; // сохраняем для последующих дубликатов
                }

                // Пересчёт SumSalary
                decimal total = 0;
                foreach (var salary in empElement.Elements("salary"))
                {
                    string? amt = salary.Attribute("amount")?.Value;
                    total += ParseAmountDecimal(amt);
                }
                empElement.SetAttributeValue("SumSalary", total.ToString(CultureInfo.InvariantCulture));
            }

            employeesDoc.Save(employeesXmlPath);
        }

        // --- GenerateEmployeesDocument (веб) ---
        public static XDocument GenerateEmployeesDocument(XDocument sourceDoc, Action<string>? onWarning = null)
        {
            var items = sourceDoc.Descendants("item").ToList();
            if (!items.Any())
            {
                onWarning?.Invoke("В файле нет элементов <item>.");
                return new XDocument(new XElement("Employees"));
            }

            var grouped = new Dictionary<string, List<(XElement Item, int LineNumber)>>();
            int lineNumber = 0;
            foreach (var item in items)
            {
                lineNumber++;
                string name = item.Attribute("name")?.Value ?? "";
                string surname = item.Attribute("surname")?.Value ?? "";
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(surname))
                {
                    onWarning?.Invoke($"Строка {lineNumber}: пропущена (отсутствуют name и surname).");
                    continue;
                }
                string key = $"{name}|{surname}";
                if (!grouped.ContainsKey(key))
                    grouped[key] = new List<(XElement, int)>();
                grouped[key].Add((item, lineNumber));
            }

            var employeesDoc = new XDocument(new XElement("Employees"));
            var root = employeesDoc.Root!;

            foreach (var kvp in grouped)
            {
                string[] parts = kvp.Key.Split('|');
                string name = parts[0];
                string surname = parts[1];
                var empElement = new XElement("Employee",
                    new XAttribute("name", name),
                    new XAttribute("surname", surname));

                var addedMounts = new HashSet<string>();
                foreach (var (item, line) in kvp.Value)
                {
                    string? mount = item.Attribute("mount")?.Value;
                    if (string.IsNullOrEmpty(mount))
                    {
                        onWarning?.Invoke($"Строка {line}: пропущена (отсутствует mount).");
                        continue;
                    }
                    string? amountStr = item.Attribute("amount")?.Value;
                    if (string.IsNullOrEmpty(amountStr))
                    {
                        onWarning?.Invoke($"Строка {line}: пропущена (отсутствует amount).");
                        continue;
                    }

                    if (addedMounts.Contains(mount))
                    {
                        onWarning?.Invoke($"Строка {line}: дубликат месяца '{mount}' для сотрудника {name} {surname}. Запись пропущена.");
                        continue;
                    }

                    empElement.Add(new XElement("salary",
                        new XAttribute("amount", amountStr),
                        new XAttribute("mount", mount)));
                    addedMounts.Add(mount);
                }

                decimal total = 0;
                foreach (var salary in empElement.Elements("salary"))
                {
                    total += ParseAmountDecimal(salary.Attribute("amount")?.Value);
                }
                empElement.SetAttributeValue("SumSalary", total.ToString(CultureInfo.InvariantCulture));
                root.Add(empElement);
            }
            return employeesDoc;
        }
    }
}