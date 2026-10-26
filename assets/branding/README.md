# ASV application and package icon

The faceted violet-and-cyan A is shared by ASV applications and NuGet packages.
Drones and RSgA applications retain their own branding.

- `asv-icon.png`: 256 × 256 NuGet package artwork.
- `asv-icon.ico`: Windows executable artwork, with 16–256 px resolutions.

`src/Directory.Build.targets` includes the PNG in each package and assigns the
ICO to desktop and console executables. Mobile applications use platform assets.
The original artwork and the macOS ICNS are maintained in `asv-avalonia/assets/branding`.
