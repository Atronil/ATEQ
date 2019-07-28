using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Ini
{
    public class INIFile
    {
        /// <summary>
        /// Create a New INI file to store or load data
        /// </summary>
            public string path;

            [DllImport("kernel32")]
            private static extern long WritePrivateProfileString(string section,
                string key, string val, string filePath);
            [DllImport("kernel32")]
            private static extern int GetPrivateProfileString(string section,
                     string key, string def, StringBuilder retVal,
                int size, string filePath);

            /// <summary>
            /// INIFile Constructor.
            /// </summary>
            /// <PARAM name="INIPath"></PARAM>
            public INIFile(string INIPath)
            {
                path = INIPath;
            }
            /// <summary>
            /// Write Data to the INI File
            /// </summary>
            /// <PARAM name="Section"></PARAM>
            /// Section name
            /// <PARAM name="Key"></PARAM>
            /// Key Name
            /// <PARAM name="Value"></PARAM>
            /// Value Name
            public void IniWriteValue(string Section, string Key, string Value)
            {
                WritePrivateProfileString(Section, Key, Value, this.path);
            }

            /// <summary>
            /// Read Data Value From the Ini File
            /// </summary>
            /// <PARAM name="Section"></PARAM>
            /// <PARAM name="Key"></PARAM>
            /// <PARAM name="Path"></PARAM>
            /// <returns></returns>
            public string IniReadValue(string Section, string Key)
            {
                StringBuilder temp = new StringBuilder(255);
                int i = GetPrivateProfileString(Section, Key, "", temp, 255, this.path);
                return temp.ToString();

            }
        }
 }


/*
 * public static string[] ReadSections(string filePath) 
{ 
    // first line will not recognize if ini file is saved in UTF-8 with BOM 
    while (true) 
    { 
        char[] chars = new char[capacity]; 
        int size = GetPrivateProfileString(null, null, "", chars, capacity, filePath); 
  
        if (size == 0) 
        { 
            return null; 
        } 
  
        if (size < capacity - 2) 
        { 
            string result = new String(chars, 0, size); 
            string[] sections = result.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries); 
            return sections; 
        } 
  
        capacity = capacity * 2; 
    } 
} 
  
public static string[] ReadKeys(string section, string filePath) 
{ 
    // first line will not recognize if ini file is saved in UTF-8 with BOM 
    while (true) 
    { 
        char[] chars = new char[capacity]; 
        int size = GetPrivateProfileString(section, null, "", chars, capacity, filePath); 
  
        if (size == 0) 
        { 
            return null; 
        } 
  
        if (size < capacity - 2) 
        { 
            string result = new String(chars, 0, size); 
            string[] keys = result.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries); 
            return keys; 
        } 
  
        capacity = capacity * 2; 
    } 
} 
  
public static string[] ReadKeyValuePairs(string section, string filePath) 
{ 
    while (true) 
    { 
        IntPtr returnedString = Marshal.AllocCoTaskMem(capacity * sizeof(char)); 
        int size = GetPrivateProfileSection(section, returnedString, capacity, filePath); 
  
        if (size == 0) 
        { 
            Marshal.FreeCoTaskMem(returnedString); 
            return null; 
        } 
  
        if (size < capacity - 2) 
        { 
            string result = Marshal.PtrToStringAuto(returnedString, size - 1); 
            Marshal.FreeCoTaskMem(returnedString); 
            string[] keyValuePairs = result.Split('\0'); 
            return keyValuePairs; 
        } 
  
        Marshal.FreeCoTaskMem(returnedString); 
        capacity = capacity * 2; 
    } 
}
 * */
