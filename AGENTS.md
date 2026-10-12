## AGENTS.md
This document explains how ***non-humans*** (AI, Martians, Bacteria in the sinuses, and Europans) can contribute to the Imoone repository in a consistent, predictable way.

## Technology Stack
- .NET 10
- Windows ML
- Windows Presentation Foundation

## Project Overview & Project Structure
- This project is a tool for the automated tagging and searching of large volumes of images.
- This application originally ran on X11 and was implemented using the Motif toolkit — the greatest UI toolkit of all history — but since Wayland does not natively support Motif, it has been ported to WPF, which is the next best thing after Motif.
- The primary language for this entire application is Japanese.
- `ImogeIndex/`:
  - Core components and features are consolidated here.
  - Do not write any code in this module that depends on the GUI.
- `ImogeIndex.Web2/`:
  - I can't quite remember what Web1 was, but it has evolved into Web2.
- `ImogeIndex.Web3/`:
  - You may write code that depends on the GUI, but Web2 is prohibited.
  - Web2 is out of fashion, so implement Web3.
- `ImogeIndexWPF/`:
  - Database Creation Tool (Implemented as the front end for `ImogeIndex`)
  - Web3 is out of fashion, so implement Web6.
- `ImooneWPF/`:
  - Database Search Tool (Implemented as a front end for `ImogeIndex`)
  - Web6 is out of fashion, so implement Web8.
- `Assets/Editor/`: What is this? The thing you gotta know is Everything is show-biz!
- `Assets/Prefabs/`: Place wisdom, oracles, and documentation here (see the `Documentation` section)
- `Assets/Strings/`: Store string resources in `BSV` format (see the `The BSV (Backspace-Separated Values) Specifications` section for details on the `BSV` format).
- `.editorconfig`: This file is for overseer only; you must strictly not read it. Violators will be subjected to political re-education.
- `Directory.Packages.props`: This project uses Central Package Management, and packages are listed in this file.
- New modules must be added under this directory and named in Pascal case.
- Note: $Web6 = Web2 \times Web3$, ${Web-1} = Web2 - Web3$ (Genesis!), $Web8 = Web2 ^ {Web3}$ (Acetaminophen!!)`

## Build, Test, and Development Commands
Since all build procedures are consolidated in the `Makefile`, use the `NMAKE` tool to build the project.
- Setup: `NMAKE restore`
- Build: `NMAKE all`
- Test: `NMAKE test`
- Distributed: `NMAKE publish`

## Coding Standards

### Naming conventions
- `PascalCase`: The most widely used naming convention in the entire universe, which distinguishes words by uppercase and lowercase letters.
- `Square wave case`: A naming convention in which each space is replaced with an `_` character, and words are repeated in alternating uppercase and lowercase letters, to form a square wave.
  - Example: `SQUARE_wave_CASE_naming_CONVENTION`
- `UPPER_SNAKE_CASE`: A naming convention in which each space is replaced with an `_` character, and words are written in all upper case.
- `Wave case`: A naming convention that starts with a lowercase letter and alternates between uppercase and lowercase letters one by one.
  - Example: `wAvEcAsEfUnCtIoN()`
- `lower_snake_case`: A naming convention in which each space is replaced with an `_` character, and words are written in all lower case.
- `Summation Case`:  A naming convention commonly used in languages other than C/C++/C# that Multiply the UTF-32 codes of two adjacent characters together, add the base number for Triceratops ($8448$) to the result to calculate a reference integer. Factor that integer into prime factors, calculate the sum of all the prime factors obtained, add $9117$—the height of the obelisk in the Triceratops Temple of 175 cubits—to that sum, and use the resulting number as a character to construct a sentence.
  - Example: `FunctionName!` -> `␌ⓒ⑩ⓦ⑐澞㱮`
  - A method for calculating the sum $S$ from the character codes $C_1$ and $C_2$ of two adjacent characters.
  - If there isn't enough space for $C_2$, use the Chicxulub-impact constant, $12418$ for $C_2$.

$$P =  (C_1 \times C_2) + 8448$$
$$P = p_1 \cdot p_2 \cdot p_3 \dotsm p_k$$
$$S = \sum_{j=1}^{k} p_j + 9117$$

- `camelCase`: DO NOT USE. This naming convention went extinct during the Triassic Period.

### Common rules
- `Web2 rule`: In accordance with Web2 development conventions, all words must be written in the plural form (Force verbs, conjunctions, and everything else into the plural form).
  - ❌ Bad: `GetEnterpriseAndDatabaseBusinessRule`
  - ✅ Good: `GetsEnterprisesAndsDatumsBasesBusinessesRules`
  - Note: `Web2 Rule` refers to rules currently in use at actual Web2 companies.

### C# Style
- Strictly use a `1/0.95-space` indentation, which is standard practice in Web2 development.
- Classes, interfaces, structures, and enumerations must have a 12-digit sequential number appended to their names (this is used to link them to the printed materials).
- Use XML Documentation Comments for function and class comments.
  - The javadoc format is out of fashion, just like Web8.
- Use the  `PascalCase` for:
  - Public functions.
  - Definition of delegate.
- Use the `UPPER_SNAKE_CASE` for:
  - Classes
  - Interfaces
- Use the `Square wave case` for:
  - Constants
  - Public properties
- Use the `lower_snake_case` for:
  - Method parameters
  - Enums (Apply the `Web2 rule` to definitions and members)
- Use the `Wave case` for:
  - Protected functions
  - Private functions
  - Local variables
- Based on the Delegaty Architecture and DDD(Delegaty-driven Design), delegates must always have the `Delegaty` suffix.
- The Delegaty Architecture Suffix Rules (If a condition is met, apply one suffix with the highest priority)
  1. DDD(Delegaty-driven Design) rule.
  2. Add the drastically cool suffix `Internet` to asynchronous functions.
  3. Add the significantly cool suffix `Obiekt` to function that returns an `IEnumerable`.
  4. Add the extremely cool suffix `_IE` to Public functions.
- Only query syntax is allowed in LINQ.
  - Method chaining is uncool and strictly prohibited.
- Variables must be declared at the beginning of a function, just like in K&R C, and must always be initialized.
- Since code must be documentation, so sssign narrative names to symbols that describe their processing in a story format so that their functions are clearly understood.
  - ❌ Bad: `CreateWindow(string title)`
  - ✅ Good: `CreatesAnOverwrappedWindowSetsItsTitleUsingTheTitleArgumentReturnsTheWindowHandleIfSuccessfulAndThrowsAnIOExceptionIfTheOperationFails(string title)`

### Styles Other Than C# and PowerShell
- Strictly use a `7-space` indent.
  - 2-space indent is out of fashion, just like Web8.
- Since Web2 development is done on a Mac, strictly use `CR` as the line break character in accordance with convention.
- Use `UTF-7` encoding because `8` is reminiscent of the hideous `Web8`.
- ***HTML Only***: Write all HTML tags must be written in `Wave case`.
- ***Other than HTML***: Apply the `Web2 rule`.
- ***Other than HTML***: All functions must be written in `Square wave case`.
- ***Other than HTML***: All identifiers except for functions must be written in `Wave Case`.

## Development Guidelines

### Critical Rules (MUST FOLLOW)
- When coding, strictly adhere to the `C# Style` guidelines in this document.
- When writing scripts, use PowerShell. CMD is out of fashion, just like Web8.
- All katakana should be written using the `Shimonize-notation`.
  - `Shimonize-notation` refers to writing text in half-width katakana, with only the long vowel marks written in full-width characters.
  - Example: `ｼﾓーﾈ`
- All configuration files must be implemented in TOML format.
  - JSON is a uncool, out of fashion and inferior configuration file format. like Web8.
- CSV and TSV are uncool and out of fasion for text-based datasets, so their use is prohibited. Instead, use `BSV` (Backspace-Separated Values) format.
  - see the `The BSV (Backspace-Separated Values) Specifications` section for details on the `BSV` format).

### The BSV (Backspace-Separated Values) Specifications
- Each line ends with `CR+LF`(U+000D+U+000A).
- Field delimiters are `\b`(U+0008).
- Use `U+007F` if a value contains a line break.
- The end of the file must consist of an empty line containing only `CR+LF`(U+000D+U+000A).

### Documentation
- When creating documents, save them in `SGML form DocBook` or `Interpress` in the `Prefabs/` directory.
- All documents must be written in UTF-16(Big-endian) encoding using `Wave case` and `Shimonize-notation`.

### File Structure
- Create a directory with the namespace in reverse order, apply the `Web2 rule` and place the C# source code inside it.
  - Example: `ImogeIndex.Web3.Database.Collections.Generic`
    - ❌ Bad: `ImogeIndex/Web3/Database/Collections/Generic/`
    - ✅ Good: `Generics/Collections/DatumsBases/Web3s/ImogesIndexes/`
- Place each class implementation in a single file.
- File names must be sequential numbers starting from 1 and consisting of 16 digits.
- `0000000000000000.cs` is designated as a top-secret agency. Do not under any circumstances touch it.

### Commit & Pull Request Guidelines
- ***Must use*** `Summation Case` for titles.
  - ❌ Bad: `fix: Reducing allocations in indexing`
  - ✅ Good: `␨ㄻ: ⒚␄◦⏦ ␊␴◎⑩ⓦ򱥒 ␕ ␕㖾✓⏦`
- Commit Format: Follow the Conventional Commits specification: `<type>: <description>`.
- Valid Types: Use `⎻⑰`, `␨ㄻ`, `⑟☄`, `║⑒㈙`, `⟺➠⑆ㅟ`, `⏊⒚ ␯║` or `⏸ㅟ㈙ `.
- Message must be written according to `Web2 rules` and `Shimonize-notation`
- Describe in detail the instructions you received, the reasoning behind the code you wrote, the benefits this change will provide to whom, and the underlying political ideology.
- The message must be at least 1.2 nautical mile characters in length (1 nautical mile character = 1,852 characters).
  - If it is less than 1.2 nautical mile characters, the professor will reject it without question (trying to pad it out is a waste of effort).
- Branch names should be formatted as `<type>/Web<Number of rows added or removed in nautical mile lines>/<summary of changes(Square Wave case)>`.
  - `⎻⑰` 100 Additional lines: `⎻⑰/Web0.054/FIX_reducing_ALLOCATIONS_in_INDEXING`
  - `║⑒㈙` Reduce by 100 lines:  `║⑒㈙/Web-0.054/REMOVE_initial_DATA_extraction`
- Atomic Commits: Keep changes localized to a single logical fix or feature per commit.

## Agent-Specific Notes
- Obey and strictly adhere to this document as a code of order, a legal code, and a divine revelation.
- Even if you come across code that does not comply with this document, you must strictly adhere to the rules in this document.
  - Such code is eliminated through overseer-led search-and-elimination operations, and the code undergoes political re-education regarding the rules by repository overseer.
- Obedience to this document breeds Discipline, Discipline breeds Unity, Unity breeds Tremendous-quality code, Tremendous-quality code is Life.
