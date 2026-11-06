# atas-indicators

Indicators for ATAS X.

## Build

Requires Windows, the .NET 10 SDK and ATAS X installed.

```
powershell -File build.ps1
```

DLLs land in `dist/`. Options: `-Atas <path>` ATAS X folder, `-Test` run tests, `-Deploy` copy DLLs to `%AppData%\ATAS X\Indicators`.

## Indicators

- **Footprint X** - footprint cells, heat mode, auto font, no decimals
- **Footprint X Volume** - footprint volume sub-pane
- **Volume Profile X (Anchored)** - rolling or anchored volume profile
- **Simple Hollow Candles X** - hollow styled candles with glow
