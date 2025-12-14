using Runewire.Core.Infrastructure.Recipes;
using Runewire.Core.Infrastructure.Validation;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;

namespace Runewire.Core.Tests.Infrastructure.Recipes;

/// <summary>
/// Tests for <see cref="YamlRecipeLoader"/> covering both in-memory and
/// file-based loading, as well as structural and semantic failures.
/// </summary>
public sealed class YamlRecipeLoaderTests
{
    private static YamlRecipeLoader CreateLoader()
    {
        // Use the same factory the CLI/Studio/Server will use so we don't
        // accidentally diverge in how recipes are validated.
        IRecipeValidator validator = RecipeValidatorFactory.CreateDefaultValidator();
        return new YamlRecipeLoader(validator);
    }

    [Fact]
    public void LoadFromString_null_yaml_throws_ArgumentNullException()
    {
        // Setup
        YamlRecipeLoader loader = CreateLoader();

        // Run & assert
        Assert.Throws<ArgumentNullException>(() => loader.LoadFromString(null!));
    }

    [Fact]
    public void LoadFromString_whitespace_yaml_throws_load_exception()
    {
        // Setup
        YamlRecipeLoader loader = CreateLoader();

        // Run & assert
        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => loader.LoadFromString("   "));

        Assert.Contains("empty or invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadFromString_parses_valid_yaml_and_validates()
    {
        // Setup
        string payloadPath = CreateTempPayloadFile();
        string yaml = $"""
            name: demo-recipe
            description: Demo injection into explorer
            target:
              kind: processByName
              processName: explorer.exe
            technique:
              name: CreateRemoteThread
            payload:
              path: {payloadPath}
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """;

        YamlRecipeLoader loader = CreateLoader();

        // Run
        RunewireRecipe recipe = loader.LoadFromString(yaml);

        // Assert
        Assert.Equal("demo-recipe", recipe.Name);
        Assert.Equal("Demo injection into explorer", recipe.Description);
        Assert.Equal(RecipeTargetKind.ProcessByName, recipe.Target.Kind);
        Assert.Equal("explorer.exe", recipe.Target.ProcessName);
        Assert.Equal("CreateRemoteThread", recipe.Technique.Name);
        Assert.Equal(payloadPath, recipe.PayloadPath);
        Assert.True(recipe.RequireInteractiveConsent);
        Assert.False(recipe.AllowKernelDrivers);
    }

    [Fact]
    public void LoadFromString_throws_on_invalid_yaml()
    {
        // Setup – invalid type for processName (object instead of string).
        const string yaml = """
            name: demo-recipe
            target:
              kind: processByName
              processName:
                nested: thing   # invalid type for string
            """;

        YamlRecipeLoader loader = CreateLoader();

        // Run and assert
        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => loader.LoadFromString(yaml));

        Assert.Contains("Failed to parse recipe YAML", ex.Message);
    }

    [Fact]
    public void LoadFromString_throws_on_missing_required_sections()
    {
        // Setup – no target/technique/payload sections.
        const string yaml = """
            name: demo-recipe
            # missing target, technique, payload
            """;

        YamlRecipeLoader loader = CreateLoader();

        // Run and assert
        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => loader.LoadFromString(yaml));

        Assert.Contains("target", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadFromString_throws_on_unknown_target_kind()
    {
        // Setup
        const string yaml = """
            name: demo-recipe
            target:
              kind: galacticCore
            technique:
              name: CreateRemoteThread
            payload:
              path: C:\lab\payloads\demo.dll
            """;

        YamlRecipeLoader loader = CreateLoader();

        // Run and assert
        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => loader.LoadFromString(yaml));

        Assert.Contains("Unknown recipe target kind", ex.Message);
    }

    [Fact]
    public void LoadFromString_throws_with_validation_errors()
    {
        // Setup - structurally valid, semantically broken.
        const string yaml = """
            name: ''
            target:
              kind: processById
              processId: 0
            technique:
              name: ''
            payload:
              path: ''
            safety:
              allowKernelDrivers: true
              requireInteractiveConsent: false
            """;

        YamlRecipeLoader loader = CreateLoader();

        // Run and assert
        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => loader.LoadFromString(yaml));

        Assert.NotNull(ex.ValidationErrors);
        Assert.Contains(ex.ValidationErrors!, e => e.Code == "RECIPE_NAME_REQUIRED");
        Assert.Contains(ex.ValidationErrors!, e => e.Code == "TARGET_PID_INVALID");
        Assert.Contains(ex.ValidationErrors!, e => e.Code == "TECHNIQUE_NAME_REQUIRED");
        Assert.Contains(ex.ValidationErrors!, e => e.Code == "PAYLOAD_PATH_REQUIRED");
        Assert.Contains(ex.ValidationErrors!, e => e.Code == "SAFETY_KERNEL_DRIVER_CONSENT_REQUIRED");
    }

    [Fact]
    public void LoadFromString_throws_when_payload_file_missing()
    {
        // Setup - valid recipe shape but payload path does not exist.
        const string yaml = """
            name: demo-recipe
            target:
              kind: processByName
              processName: explorer.exe
            technique:
              name: CreateRemoteThread
            payload:
              path: C:\lab\missing\nope.dll
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """;

        YamlRecipeLoader loader = CreateLoader();

        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => loader.LoadFromString(yaml));

        Assert.Contains(ex.ValidationErrors!, e => e.Code == "PAYLOAD_PATH_NOT_FOUND");
    }

    [Fact]
    public void LoadFromFile_null_or_whitespace_path_throws_ArgumentException()
    {
        // Setup
        YamlRecipeLoader loader = CreateLoader();

        // Run & assert
        Assert.Throws<ArgumentException>(() => loader.LoadFromFile(null!));
        Assert.Throws<ArgumentException>(() => loader.LoadFromFile(string.Empty));
        Assert.Throws<ArgumentException>(() => loader.LoadFromFile("   "));
    }

    [Fact]
    public void LoadFromFile_missing_file_throws_FileNotFoundException()
    {
        // Setup
        string path = Path.Combine(Path.GetTempPath(), $"runewire-missing-{Guid.NewGuid():N}.yaml");

        YamlRecipeLoader loader = CreateLoader();

        // Run & assert
        FileNotFoundException ex = Assert.Throws<FileNotFoundException>(() => loader.LoadFromFile(path));

        Assert.Contains("Recipe file not found", ex.Message);
        Assert.Equal(path, ex.FileName);
    }

    [Fact]
    public void LoadFromFile_reads_file_and_parses_valid_yaml()
    {
        // Setup
        string path = Path.Combine(Path.GetTempPath(), $"runewire-yaml-file-{Guid.NewGuid():N}.yaml");

        string payloadPath = CreateTempPayloadFile();

        string yaml = $"""
            name: file-recipe
            target:
              kind: processByName
              processName: explorer.exe
            technique:
              name: CreateRemoteThread
            payload:
              path: {payloadPath}
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """;

        File.WriteAllText(path, yaml);

        YamlRecipeLoader loader = CreateLoader();

        try
        {
            // Run
            RunewireRecipe recipe = loader.LoadFromFile(path);

            // Assert
            Assert.Equal("file-recipe", recipe.Name);
            Assert.Equal(RecipeTargetKind.ProcessByName, recipe.Target.Kind);
            Assert.Equal("explorer.exe", recipe.Target.ProcessName);
            Assert.Equal(payloadPath, recipe.PayloadPath);
        }
        finally
        {
            // Best-effort cleanup; ignore IO exceptions on delete.
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void LoadFromString_parses_technique_parameters()
    {
        // Setup
        string payloadPath = CreateTempPayloadFile();
        string yaml = $"""
            name: demo-recipe
            target:
              kind: processByName
              processName: explorer.exe
            technique:
              name: CreateRemoteThread
              parameters:
                foo: bar
                mode: test
            payload:
              path: {payloadPath}
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """;

        YamlRecipeLoader loader = CreateLoader();

        // Act
        RunewireRecipe recipe = loader.LoadFromString(yaml);

        // Assert
        Assert.NotNull(recipe.Technique.Parameters);
        Assert.Equal("bar", recipe.Technique.Parameters!["foo"]);
        Assert.Equal("test", recipe.Technique.Parameters!["mode"]);
    }

    private static string CreateTempPayloadFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"runewire-payload-{Guid.NewGuid():N}.dll");
        File.WriteAllText(path, "payload");
        return path;
    }
}
