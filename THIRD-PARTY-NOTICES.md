# Third-party notices

Image Converter uses open-source components. Their respective licenses remain
in effect; the project license does not replace them.

## Magick.NET 14.16.0

Copyright © Dirk Lemstra and contributors. Licensed under Apache-2.0.

- Source: <https://github.com/dlemstra/Magick.NET>
- License: <https://github.com/dlemstra/Magick.NET/blob/main/License.txt>

## ImageMagick and native delegates

Magick.NET ships ImageMagick and native codec delegates. ImageMagick is
licensed under the ImageMagick License. The complete notice bundle shipped by
the exact Magick.NET package version is included at
[`licenses/MagickNET-ThirdPartyNotices.txt`](licenses/MagickNET-ThirdPartyNotices.txt).
It contains the Apache-2.0 text for Magick.NET plus the licenses and notices
for ImageMagick and the bundled native codec delegates.

The native distribution can include additional libraries such as libheif and
libde265. Their notices and license terms are distributed by the corresponding
Magick.NET package and must remain included in binary releases.

- ImageMagick license: <https://imagemagick.org/script/license.php>
- libheif: <https://github.com/strukturag/libheif>
- libde265: <https://github.com/strukturag/libde265>

## HEIC test fixture

The integration-test file `libheif-example.heic` comes from the upstream
libheif `examples` directory and is licensed under the MIT License. Its source,
license link and SHA-256 checksum are documented in
[`tests/ImageConverter.Core.Tests/Assets/README.md`](tests/ImageConverter.Core.Tests/Assets/README.md).

## Windows App SDK

Copyright © Microsoft Corporation. Licensed under the MIT License.

- Source and license: <https://github.com/microsoft/WindowsAppSDK>

## .NET Runtime

Copyright © .NET Foundation and contributors. Licensed under the MIT License.

- Source and license: <https://github.com/dotnet/runtime>

Binary redistributors are responsible for retaining all notices shipped with
the published application and for reviewing the licenses of the exact package
versions used for a release.
