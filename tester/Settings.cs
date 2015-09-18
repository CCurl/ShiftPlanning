using System;
using System.Collections.Generic;
using System.IO;

public class Settings
{
    public Settings(string iniFile)
    {
        Entries = new List<IniValue>();
        IniFile = iniFile;
    }

    private List<IniValue> Entries;

    private void ParseIniFile()
    {
        Entries.Clear();
        string section = string.Empty;
        var lines = File.ReadAllLines(IniFile);
        for (int i = 0; i < lines.GetLength(0); i++)
        {
            string tmp = lines[i];
            if (tmp.StartsWith(";"))
            {
                continue;
            }

            if (tmp.StartsWith("[") && tmp.EndsWith("]"))
            {
                section = tmp.Substring(1, tmp.Length - 2);
            }
            else
            {
                int pos = tmp.IndexOf('=');
                if (pos > 1)
                {
                    string key = tmp.Substring(0, pos);
                    string val = tmp.Substring(pos+1);
                    if (!string.IsNullOrEmpty(section) && !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                    {
                        Entries.Add(new IniValue(section, key, val));
                    }
                }
            }
        }
    }

    public string GetValue(string Section, string Key)
    {
        foreach (var e in Entries)
        {
            if ((e.Section == Section) && (e.Key == Key))
            {
                return e.Value;
            }
        }
        return string.Empty;
    }

    private string _iniFile;
    public string IniFile {
        get { return _iniFile; }
        set
        {
            if (File.Exists(value))
            {
                _iniFile = value;
                ParseIniFile();
            }
        }
    }

    class IniValue
    {
        public string Section;
        public string Key;
        public string Value;

        public IniValue(string section, string key, string value)
        {
            this.Section = section;
            this.Key = key;
            this.Value = value;
        }
    }
};
