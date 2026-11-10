# NeuralNet3D

A WinForms app: draw a digit with the mouse on the left, and on the right see a 3D
visualization of the neural network on "bare" OpenGL (fixed-function 1.1 via P/Invoke,
no OpenTK/SharpGL/GLU).

## Requirements

- Windows (x64)
- .NET 10 SDK
- A GPU driver with OpenGL (only 1.1 is used)
- Internet on the **first** run — to download MNIST (~11 MB)

No NuGet dependencies: only `opengl32.dll` / `gdi32.dll` / `user32.dll`, plus .NET's
`HttpClient` to fetch the dataset.

## Screenshot
![Screenshot](screenshot.png)

## Running

```
cd NeuralNet3D
dotnet run -c Release
```

On the **first** run the app downloads MNIST into `%LOCALAPPDATA%\NeuralNet3D\mnist`,
trains on it (a few seconds; progress and test accuracy are shown at the bottom left)
and saves the weights to `%LOCALAPPDATA%\NeuralNet3D\weights.bin`. After that the
weights are simply loaded from the cache; delete the folder to train again.

> If there is no internet or MNIST fails to download, the app falls back to a synthetic
> dataset (digits rendered with system fonts).

## Controls

- **Left panel:** draw with the left mouse button; a digit may take several strokes.
  It is recognized after every stroke. `Clear` or `Esc` wipes the canvas.
- **3D view:** drag to rotate (this stops the auto-rotation), mouse wheel to zoom,
  double-click to resume the auto-rotation.

## How it works

- **Network:** 784 → 64 → 32 → 10, ReLU hidden layers and a softmax output, trained with
  plain per-sample SGD on cross-entropy with label smoothing, 15 epochs, ~97% on the MNIST
  test set. Blank pixels and silent ReLU neurons contribute nothing, so the forward pass
  and the weight updates skip them and run on the rest with SIMD.
- **Preprocessing:** the drawing is cropped to its ink, scaled so the longer side spans
  20 pixels, centred by its centre of mass in a 28×28 field and slightly blurred: the way
  the MNIST digits themselves were prepared.
- **Visualization:** neurons are lit spheres, the input digit glows on the 28×28 input
  grid, the strongest connections are drawn as additive lines (blue for positive
  weights, red for negative), and after each recognition a wave of activation runs
  through the layers to the winning output.

## Support

If you found this project interesting or useful, you can support my work:

[![GitHub Sponsors](https://img.shields.io/github/sponsors/makarov-mm?style=flat&logo=github)](https://github.com/sponsors/makarov-mm)
