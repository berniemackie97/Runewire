using Runewire.Core.Infrastructure.Recipes;
using Runewire.Core.Infrastructure.Validation;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;

namespace Runewire.Core.Tests.Infrastructure.Recipes;

/// <summary>
/// Tests for <see cref="JsonRecipeLoader"/> covering parsing and validation.
/// </summary>
public sealed class JsonRecipeLoaderTests
{
    private static JsonRecipeLoader CreateLoader()
    {
        IRecipeValidator validator = RecipeValidatorFactory.CreateDefaultValidator();
        return new JsonRecipeLoader(validator);
    }

    [Fact]
    public void LoadFromString_parses_valid_json_and_validates()
    {
        // Setup
        string payloadPath = CreateTempPayloadFile();
        string jsonTemplate = """
            {
              "name": "demo-recipe",
              "description": "Demo injection into explorer",
              "target": {
                "kind": "processByName",
                "processName": "explorer.exe"
              },
              "technique": {
                "name": "CreateRemoteThread"
              },
              "payload": {
                "path": "__PAYLOAD__"
              },
              "safety": {
                "requireInteractiveConsent": true,
                "allowKernelDrivers": false
              }
            }
            """;
        string json = jsonTemplate.Replace("__PAYLOAD__", EscapeForJson(payloadPath), StringComparison.Ordinal);

        JsonRecipeLoader loader = CreateLoader();

        // Act
        RunewireRecipe recipe = loader.LoadFromString(json);

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
    public void LoadFromString_throws_on_invalid_json()
    {
        // Setup - invalid JSON (missing closing brace).
        const string json = """
            {
              "name": "demo-recipe"
            """;

        JsonRecipeLoader loader = CreateLoader();

        // Act & Assert
        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => loader.LoadFromString(json));
        Assert.Contains("Failed to parse recipe JSON", ex.Message);
    }

    [Fact]
    public void LoadFromString_throws_on_unknown_target_kind()
    {
        const string json = """
            {
              "name": "demo-recipe",
              "target": {
                "kind": "galacticCore"
              },
              "technique": { "name": "CreateRemoteThread" },
              "payload": { "path": "C:\\lab\\payloads\\demo.dll" }
            }
            """;

        JsonRecipeLoader loader = CreateLoader();

        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => loader.LoadFromString(json));

        Assert.Contains("Unknown recipe target kind", ex.Message);
    }

    [Fact]
    public void LoadFromString_throws_with_validation_errors()
    {
        // Setup - structurally valid, semantically broken.
        const string json = """
            {
              "name": "",
              "target": {
                "kind": "processById",
                "processId": 0
              },
              "technique": { "name": "" },
              "payload": { "path": "" },
              "safety": {
                "allowKernelDrivers": true,
                "requireInteractiveConsent": false
              }
            }
            """;

        JsonRecipeLoader loader = CreateLoader();

        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => loader.LoadFromString(json));

        Assert.NotNull(ex.ValidationErrors);
        Assert.Contains(ex.ValidationErrors!, e => e.Code == "RECIPE_NAME_REQUIRED");
        Assert.Contains(ex.ValidationErrors!, e => e.Code == "TARGET_PID_INVALID");
        Assert.Contains(ex.ValidationErrors!, e => e.Code == "TECHNIQUE_NAME_REQUIRED");
        Assert.Contains(ex.ValidationErrors!, e => e.Code == "PAYLOAD_PATH_REQUIRED");
        Assert.Contains(ex.ValidationErrors!, e => e.Code == "SAFETY_KERNEL_DRIVER_CONSENT_REQUIRED");
    }

    [Fact]
    public void LoadFromFile_reads_file_and_parses_valid_json()
    {
        string path = Path.Combine(Path.GetTempPath(), $"runewire-json-file-{Guid.NewGuid():N}.json");

        string payloadPath = CreateTempPayloadFile();

        string jsonTemplate = """
            {
              "name": "file-recipe",
              "target": {
                "kind": "processByName",
                "processName": "explorer.exe"
              },
              "technique": { "name": "CreateRemoteThread" },
              "payload": { "path": "__PAYLOAD__" },
              "safety": {
                "requireInteractiveConsent": true,
                "allowKernelDrivers": false
              }
            }
            """;
        string json = jsonTemplate.Replace("__PAYLOAD__", EscapeForJson(payloadPath), StringComparison.Ordinal);

        File.WriteAllText(path, json);

        JsonRecipeLoader loader = CreateLoader();

        try
        {
            RunewireRecipe recipe = loader.LoadFromFile(path);

            Assert.Equal("file-recipe", recipe.Name);
            Assert.Equal(RecipeTargetKind.ProcessByName, recipe.Target.Kind);
            Assert.Equal("explorer.exe", recipe.Target.ProcessName);
            Assert.Equal(payloadPath, recipe.PayloadPath);
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void LoadFromString_throws_when_payload_file_missing()
    {
        const string json = """
            {
              "name": "demo-recipe",
              "target": { "kind": "processByName", "processName": "explorer.exe" },
              "technique": { "name": "CreateRemoteThread" },
              "payload": { "path": "C:\\lab\\missing\\nope.dll" },
              "safety": { "requireInteractiveConsent": true, "allowKernelDrivers": false }
            }
            """;

        JsonRecipeLoader loader = CreateLoader();

        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => loader.LoadFromString(json));

        Assert.Contains(ex.ValidationErrors!, e => e.Code == "PAYLOAD_PATH_NOT_FOUND");
    }

    private static string CreateTempPayloadFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"runewire-payload-{Guid.NewGuid():N}.dll");
        File.WriteAllText(path, "payload");
        return path;
    }

    private static string EscapeForJson(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal);
}
