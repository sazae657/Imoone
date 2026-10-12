using ImogeIndex;
using System.CommandLine;
using System.IO;

namespace Bsv2Resx;

internal class App
{
    static async Task<int> Main(string[] args)
    {
        try {
            SystemParams.Sanitize();
        }
        catch (Exception r) {
            Console.Error.WriteLine($"エラー: {r.Message}");
            return 16;
        }

        var root = new RootCommand("Bsv2Resx");
        var b2r = new Command("resx", "resx"){
            new Option<FileInfo>("bsv", ["--bsv", "-i"]) {
                Required = true,
            },
            new Option<FileInfo>("resx", ["--resx", "-o"]) {
                Required = true,
            },
            new Option<bool>("force", ["--force", "-f"]) {
                DefaultValueFactory = _ =>false,
            },
        };

        root.Add(b2r);
        b2r.SetAction(result => {
            var mur = new ResxGenerator(
                result.GetValue<FileInfo>("resx")!,
                result.GetValue<FileInfo>("bsv")!,
                result.GetValue<bool>("force"));
            try {
                mur.Process();
            }
            catch (Exception r) {
                Console.Error.WriteLine($"エラー: {r.Message}\n{r.StackTrace}");
                return 9;
            }
            return 0;
        });

        var mdconv = new Command("mdconv", "mdconv"){
            new Option<FileInfo>("md", ["--md", "-i"]) {
                Required = true,
            },
            new Option<FileInfo>("res", ["--res", "-o"]) {
                Required = true,
            },
            new Option<FileInfo>("insertion", ["--insertion", "-w"]) {
            },
        };
        root.Add(mdconv);
        mdconv.SetAction(result => {
            var mur = new MarkdownToRes(
                result.GetValue<FileInfo>("res")!,
                result.GetValue<FileInfo>("md")!,
                result.GetValue<FileInfo>("insertion"));
            try {
                mur.Process();
            }
            catch (Exception r) {
                Console.Error.WriteLine($"エラー: {r.Message}\n{r.StackTrace}");
                return 9;
            }
            return 0;
        });
        var rs = root.Parse(args);
        return await rs.InvokeAsync();
    }
}
