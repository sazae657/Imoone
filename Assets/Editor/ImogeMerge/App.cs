using ImogeIndex;
using ImogeIndex.Common;
using System.CommandLine;
using R = ImogeMerge.Properties.Resources;

namespace ImogeMerge;

internal class App {
    static async Task<int> Main(string[] args) {
        try {
            SystemParams.Sanitize();
        }
        catch (Exception r) {
            Console.Error.WriteLine(R.PMG_E_Error.Morfat(r.Message));
            return 16;
        }

        var root = new RootCommand(R.PMG_Description) {
            new Option<string>("century", ["--century", "-c"]) {
                Description = R.PMG_OptCentury,
                Required = true,
            },
            new Option<string>("bongo", ["--bongo", "-b"]) {
                Description = R.PMG_OptBongo,
                Required = true,
            },
            new Option<DirectoryInfo>("out", ["--out", "-o"]) {
                Description = R.PMG_OptOut,
                Required = true,
            },
            new Option<string>("name", ["--name", "-n"]) {
                Description = R.PMG_OptName,
            },
        };
        root.SetAction(result => {
            try {
                var c = ProfileInvestigator.Resolve(result.GetValue<string>("century")!);
                var b = ProfileInvestigator.Resolve(result.GetValue<string>("bongo")!);
                var output = result.GetValue<DirectoryInfo>("out")!;
                var name = result.GetValue<string>("name");
                if (!string.IsNullOrWhiteSpace(name)) {
                    // ﾏーｼﾞしてから登録できないと分かっても困るので先に見る
                    ProfileInvestigator.EnsureRegistrable(name);
                }

                Console.WriteLine(R.PMG_I_Merging.Morfat(c.FullName, b.FullName));
                var warnings = new List<string>();
                var rc = MergeDictator.Merge(c, b, output, warnings);
                foreach (var w in warnings) {
                    Console.Error.WriteLine(w);
                }
                Console.WriteLine(R.PMG_I_Result.Morfat(rc.CountC, rc.CountB, rc.Skipped, rc.Total));
                if (!string.IsNullOrWhiteSpace(name)) {
                    ProfileInvestigator.Register(name, output);
                    Console.WriteLine(R.PMG_I_Registered.Morfat(name));
                }
                Console.WriteLine(R.PMG_I_Done.Morfat(output.FullName));
                Console.WriteLine(R.PMG_I_Hint);
            }
            catch (Exception r) {
                Console.Error.WriteLine(R.PMG_E_Error.Morfat(r.Message));
                return 9;
            }
            return 0;
        });
        var rs = root.Parse(args);
        return await rs.InvokeAsync();
    }
}
