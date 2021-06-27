# Pixiv Client WPF version

This application is a Pixiv third client for Windows, using .Net WPF technical.
It support visit pixiv through custom http/https proxy.

## Develop Environment 

1. Visual Studio Express 2015 for Desktop
1. Expression.Blend.Sdk
1. Microsoft.WindowsAPICodePark
1. Microsoft.Xaml.Behaviors.Wpf
1. NLog
1. NBug
1. Prism
1. Mahapps.Metro
1. Newtonsoft.Json
1. MouseKeyHook
1. Dfust.Hotkeys
1. WPFNotification
1. WriteableBitmapEx
1. Pixeez (Get from [PixivUniversal](https://github.com/PixeezPlusProject/PixivUniversal) PixivAPI but modified by me)

## Features

1. Proxy supported, for API and/or download
1. Must Login every hours (maybe)
1. User Name/Pass not saved to local
1. Support Private Following / Favorite
1. Support simple visited history for illust & user, but maybe slower when has large amount (>150)
1. Download with break continuation
1. Prefetching thumbnail/avatar/preview
1. Proccessing cross-process commands
1. Supported Ugoira download, and calling ffmpeg convert it to webm before view

## Known Bugs

1. Thumbnail list view maybe hang-up UI when more than 150 images

## ToDo

1. ...
1. ...
1. ...

