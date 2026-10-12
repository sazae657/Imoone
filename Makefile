SLN = Imoone.slnx
B2R = -verbosity:quiet -p:Platform=x64 --project "Assets\Editor\Bsv2Resx\Bsv2Resx.csproj" --
STRINGS = "Assets\Strings\Resources"
DN = dotnet

all: resx
	$(DN) build -target:ImooneWPF -verbosity:minimal -p:Platform=x64

clean:
	$(DN) clean -verbosity:minimal

resx:
	$(DN) run $(B2R) resx --bsv $(STRINGS)\ImogeIndex.bsv --resx "ImogeIndex\Properties\Resources.resx"
	$(DN) run $(B2R) resx --bsv $(STRINGS)\ImogeIndex.Web3.bsv --resx "ImogeIndex.Web3\Properties\Resources.resx"
	$(DN) run $(B2R) resx --bsv $(STRINGS)\ImogeIndexWPF.bsv --resx "ImogeIndexWPF\Properties\Resources.resx"
	$(DN) run $(B2R) resx --bsv "$(STRINGS)\ImooneWPF.bsv" --resx "ImooneWPF\Properties\Resources.resx"
    $(DN) run $(B2R) resx --bsv "$(STRINGS)\ImogeMerge.bsv" --resx "Assets\Editor\ImogeMerge\Properties\Resources.resx"

restore:
	$(DN) restore

test:
	$(DN) test -verbosity:minimal -p:Platform=x64

publish: all
	$(DN) publish ImooneWPF\ImooneWPF.csproj -r win-x64 -p:PublishProfile=FolderProfile -c Release
