using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("deleting an edited frame invalidates stale history", DeleteFrameInvalidatesHistory);
Run("reordering an edited frame does not replay onto another frame", MoveFramePreservesTargets);
Run("deleting an edited layer invalidates stale history", DeleteLayerInvalidatesHistory);
Run("reordering an edited layer does not replay onto another layer", MoveLayerPreservesTargets);
Run("out-of-bounds selection moves are clipped safely", MoveRectClipsBounds);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} pixel-editor core regression tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} pixel-editor core regression tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {name}: {exception.Message}");
    }
}

static void DeleteFrameInvalidatesHistory()
{
    var doc = new PixelEditorService(2, 2);
    doc.AddFrame();
    Paint(doc, 0, 0, 0xFF112233u);
    Assert(doc.CanUndo, "the edit did not enter undo history");

    doc.DeleteFrame();
    Assert(!doc.CanUndo && !doc.CanRedo, "frame deletion retained stale pixel history");
    doc.Undo();
}

static void MoveFramePreservesTargets()
{
    var doc = new PixelEditorService(2, 2);
    doc.ActiveLayer.Pixels[0] = 0xFF445566u;
    doc.AddFrame();
    Paint(doc, 0, 0, 0xFF112233u);

    doc.MoveFrame(1, 0);
    Assert(!doc.CanUndo && !doc.CanRedo, "frame reorder retained index-based history");
    doc.Undo();
    Equal(0xFF112233u, doc.Frames[0].Layers[0].Pixels[0], "undo touched the reordered frame after history reset");
    Equal(0xFF445566u, doc.Frames[1].Layers[0].Pixels[0], "undo replayed onto another frame");
}

static void DeleteLayerInvalidatesHistory()
{
    var doc = new PixelEditorService(2, 2);
    doc.AddLayer();
    Paint(doc, 0, 0, 0xFF112233u);
    Assert(doc.CanUndo, "the edit did not enter undo history");

    doc.DeleteLayer();
    Assert(!doc.CanUndo && !doc.CanRedo, "layer deletion retained stale pixel history");
    doc.Undo();
}

static void MoveLayerPreservesTargets()
{
    var doc = new PixelEditorService(2, 2);
    doc.ActiveLayer.Pixels[0] = 0xFF445566u;
    doc.AddLayer();
    Paint(doc, 0, 0, 0xFF112233u);

    doc.MoveLayer(-1);
    Assert(!doc.CanUndo && !doc.CanRedo, "layer reorder retained index-based history");
    doc.Undo();
    Equal(0xFF112233u, doc.ActiveLayer.Pixels[0], "undo touched the reordered layer after history reset");
    Equal(0xFF445566u, doc.ActiveFrame.Layers[1].Pixels[0], "undo replayed onto another layer");
}

static void MoveRectClipsBounds()
{
    var doc = new PixelEditorService(2, 2);
    doc.ActiveLayer.Pixels[0] = 0xFF112233u;

    doc.MoveRect(0, 0, 4, 0, 1, 0);

    Equal(0u, doc.ActiveLayer.Pixels[0], "in-bounds source was not cleared");
    Equal(0xFF112233u, doc.ActiveLayer.Pixels[1], "in-bounds source was not moved");
}

static void Paint(PixelEditorService doc, int x, int y, uint bgra)
{
    var action = doc.BeginStroke();
    doc.SetPixel(action, x, y, bgra);
    doc.CommitStroke(action);
}

static void Equal(uint expected, uint actual, string message)
{
    if (expected != actual) throw new InvalidOperationException($"{message}; expected 0x{expected:X8}, actual 0x{actual:X8}.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
