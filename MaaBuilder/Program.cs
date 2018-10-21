using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Evaluation;

namespace MaaBuilder {
  static class Exts {
    public static TV Get<TK, TV>(this IDictionary<TK, TV> d, TK k) {
      return d.Get(k, default(TV));
    }
    public static TV Get<TK, TV>(this IDictionary<TK, TV> d, TK k, TV dflt) {
      TV x;
      return (d.TryGetValue(k, out x)) ? x : dflt;
    }
  }


  /*
   REQUIREMENTS (bash syntax) -- only searches in Framework64, but it works if I just copy it there
   - cp C:\Windows\Microsoft.NET\Framework{,64}\v3.5\Microsoft.CompactFramework.CSharp.targets
   - cp C:\Windows\Microsoft.NET\Framework{,64}\v3.5\Microsoft.CompactFramework.Common.targets
     */

  class Program {
    static Dictionary<string, string> parseArgs(string[] args) {
      //each arg is expected to be: name=<value>
      // -- calue can contain =: only first = is considred
      // - will throw if name is unrecognized
      // - if no '=' -- represents just a flag
      // - they don't start with - or something; every arg must be like this

      var d = new Dictionary<string, string>();
      foreach (var a in args) {
        if (a == null || string.IsNullOrWhiteSpace(a))
          continue;

        var nv = a.Trim().Split(new[] { '=' }, 2);
        if (nv.Length == 1)
          d[nv[0]] = ""; //flag is there
        else if (nv.Length == 2)
          d[nv[0]] = nv[1];
        else {
          throw new Exception("Could not parse arg: '" + a + "'");
        }
      }
      return d;
    }

    static int Main(string[] args) {

      var pa = parseArgs(args);

      var help = @" --- builder args ---
proj=           pah to .csproj / ...
variant=        adds #define VARIANT_<this.to_upper>
Configuration=  /p:Configuration
wait_key=       after build done: will wait for key to terminate
out=            path to output files into
tools_version=  //default: 3.5
wince=          set WinCE flag

";
      if (pa.ContainsKey("help")) {
        Console.WriteLine(help);
        return 0;
      }
      

      //TODO: args: 
      // - variant potentaily used in more stuff
      // - output path
      // - extra defines //fixes missing leading ;
      // -- define=[;]D[;D;D]
      // 

      var ok = PBuild(pa);

      if (!ok || pa.ContainsKey("wait_key")) {
        foreach (var a in pa) {
          Console.WriteLine("{0}={1}", a.Key, a.Value);
        }

        Console.WriteLine((ok ? "Done." : "ERRORS!") + " Press any key to exit. --------------------------------------");
        Console.ReadKey();
      }

      return ok ? 0 : 1;
    }


    public static bool PBuild(Dictionary<string, string> args) {

      var p = new Project(args.Get("proj"), new Dictionary<string, string>(), args.Get("tools_version", "3.5"));
      //var p = new Project(args.Get("proj"), new Dictionary<string, string>(), "14.0");
      //var p = new Project(args.Get("proj"));

      var cgtion = args.Get("Configuration", "Release");

      p.SetGlobalProperty("Configuration", cgtion);
      p.SetGlobalProperty("DebugSymbols", "false"); //generally: I don't send them anyway: no point in making them
      p.SetGlobalProperty("DebugType", "none");
      p.SetGlobalProperty("Optimize", "true");

      //if (args.ContainsKey("wince"))
      //  p.SetGlobalProperty("TargetCompactFramework", "3.5");  //WRONG: CF != CE
      //else
      //  p.SetGlobalProperty("TargetFrameworkVersion", "4.5");

      //if (!args.ContainsKey("wince"))
      //  p.SetGlobalProperty("TargetFrameworkVersion", "4.5");


      p.SetGlobalProperty("VisualStudioVersion", "14.0");

      //case sensitive!
      var defines = (p.GetProperty("DefineConstants").UnevaluatedValue ?? "")
        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();


      if (args.ContainsKey("variant")) {
        var v = (args.Get("variant") ?? "").ToUpperInvariant();
        defines.Add("VARIANT");
        defines.Add("VARIANT_" + v);
      }

      if (args.ContainsKey("define")) {
        var v = (args.Get("define") ?? "").ToUpperInvariant()
          .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        defines.AddRange(v);
      }

      if (args.ContainsKey("out")) {
        var v = (args.Get("out") ?? "");
        p.SetGlobalProperty("OutputPath", v);
      }

      { //fixup defines
        if (cgtion != "Debug")
          defines.Remove("DEBUG");

        defines.Remove("$(PlatformFamilyName)"); //causes warnings and cannot be used anyway - maybe something old?
        if (args.ContainsKey("wince"))
          defines.Add("WindowsCE");

        defines.Add("MAA_BUILD");
      }

      args[":defines"] = string.Join(";", defines);
      p.SetGlobalProperty("DefineConstants", string.Join(";", defines));

      p.ReevaluateIfNecessary();

      Console.WriteLine("clean...");

      //.Build() actually just runs a target
      //without Clean won't recompile if nothing changed - even if Defines are different
      // - also can cause other problems: any 'deploy' ALWAYS rebuild
      p.Build("Clean", new[] { new ConsoleLogger() });
      //Console.WriteLine("building...");
      return p.Build(new ConsoleLogger());
    }
  }


}
