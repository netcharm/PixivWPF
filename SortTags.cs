//css_args /co:/win32icon:./favicon.ico
//css_co /win32icon:./favicon.ico

////css_reference PresentationFramework.dll
//css_reference Newtonsoft.Json.dll

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json;

[assembly: AssemblyTitle("Sort JSON file")]
//[assembly: AssemblyDescription("Sort JSON file")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("NetCharm")]
[assembly: AssemblyProduct("Sort JSON file")]
[assembly: AssemblyCopyright("Copyright NetCharm © 2020")]
[assembly: AssemblyTrademark("NetCharm")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

namespace netcharm
{
	static class MyScript
	{
		//void Sort(JObject jObj)
		//{
		//	var props = jObj.Properties().ToList();
		//	foreach (var prop in props)
		//	{
		//		prop.Remove();
		//	}

		//	foreach (var prop in props.OrderBy(p=>p.Name))
		//	{
		//		jObj.Add(prop);
		//		if(prop.Value is JObject)
		//			Sort((JObject)prop.Value);
		//	}
		//}
	
		public static void Main(string[] args)
		{
            var title = Console.Title;
			if (args.Length < 1) return;
			
			var tags_json = args[0];
			
			var tags_i = File.ReadAllText(tags_json);
			var tags = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags_i);
			var keys = tags.Keys.ToList();
			foreach(var k in keys)
			{
				tags[k.Trim()] = tags[k].Trim();
			}
			var sd = new SortedDictionary<string, string>(tags);
			//Sort(tags);
			var tags_o = JsonConvert.SerializeObject(sd, Formatting.Indented);
            File.WriteAllText(tags_json, tags_o, new UTF8Encoding(true));

			Console.Title = title;
		}
	}
}  


