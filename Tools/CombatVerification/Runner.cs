using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;

public static class CombatVerification
{
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) =>
        {
            string path = Path.Combine(AppContext.BaseDirectory, new AssemblyName(eventArgs.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        var types = new[] { typeof(BattleTempoTests), typeof(BattleEncounterTests), typeof(AchievementRegisterTests), typeof(BattlePollutionTests), typeof(BattleSurvivalTests), typeof(BattleCrisisTests), typeof(BattleRhythmTests), typeof(BattleChordTests), typeof(BattleMeasureTests), typeof(BattleSpatialCombatTests),
            typeof(BattleCompositionTests), typeof(ConscriptionTests), typeof(BattleSpatialStateTests), typeof(CombatTests), typeof(SymphonyTests), typeof(BattleVerdictTests), typeof(WorldBattleTests) }.ToList();
        if (args.Contains("--world")) types.AddRange(new[] { typeof(WorldPursuitTests), typeof(WorldUnitTests), typeof(WorldCivilizationTests), typeof(WorldGenerationTests) });
        if (args.Contains("--ui")) types.Add(typeof(UiLayoutTests));
        int passed = 0, failed = 0, skipped = 0;
        var report = new XElement("testsuites");
        foreach (var type in types)
        {
            int suitePassed = 0, suiteFailed = 0, suiteSkipped = 0;
            var suite = new XElement("testsuite", new XAttribute("name", type.Name));
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var cases = method.GetCustomAttributes<TestCaseAttribute>().Select(c => c.Arguments).ToList();
                if (cases.Count == 0 && method.GetCustomAttribute<TestAttribute>() != null) cases.Add(Array.Empty<object>());
                foreach (var arguments in cases)
                {
                    string name = method.Name + (arguments.Length > 0 ? "(" + string.Join(",", arguments.Select(a => a?.ToString())) + ")" : "");
                    var test = new XElement("testcase", new XAttribute("classname", type.Name), new XAttribute("name", name));
                    if (type == typeof(CombatTests) && method.Name == "RealBestiary_PureLightBeingsCarryABinding")
                    { test.Add(new XElement("skipped", "Requires the native Unity Resources loader; run in Editor.")); suite.Add(test); skipped++; suiteSkipped++; continue; }
                    object instance = null;
                    Exception failure = null;
                    try
                    {
                        instance = Activator.CreateInstance(type);
                        foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttribute<SetUpAttribute>() != null)) setup.Invoke(instance, null);
                        method.Invoke(instance, arguments);
                    }
                    catch (Exception error) { failure = error is TargetInvocationException invocation ? invocation.InnerException : error; }
                    finally
                    {
                        if (instance != null) foreach (var teardown in type.GetMethods().Where(m => m.GetCustomAttribute<TearDownAttribute>() != null))
                            try { teardown.Invoke(instance, null); } catch (Exception error) { failure = failure ?? error; }
                    }
                    if (failure == null) { passed++; suitePassed++; }
                    else { Console.WriteLine("FAIL " + type.Name + "." + name + "\n" + failure); test.Add(new XElement("failure", failure.ToString())); failed++; suiteFailed++; }
                    suite.Add(test);
                }
            }
            suite.Add(new XAttribute("tests", suitePassed + suiteFailed + suiteSkipped), new XAttribute("failures", suiteFailed), new XAttribute("skipped", suiteSkipped));
            report.Add(suite);
            Console.WriteLine(type.Name + ": " + suitePassed + " passed, " + suiteFailed + " failed, " + suiteSkipped + " skipped");
        }
        report.Add(new XAttribute("tests", passed + failed + skipped), new XAttribute("failures", failed), new XAttribute("skipped", skipped));
        new XDocument(report).Save(Path.Combine(AppContext.BaseDirectory, "..", "offline-results.xml"));
        Console.WriteLine("TOTAL: " + passed + " passed, " + failed + " failed, " + skipped + " skipped");
        return failed == 0 ? 0 : 1;
    }
}
