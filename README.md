# LocosLab Virtual Camera for Windows 11

This repository contains an installer with a programmable virtual camera for Windows Media Foundation that I have written to help a couple of students with one of their projects at the University Duisburg-Essen. The virtual camera provides a .NET assembly that enables you to build .NET applications providing a camera feed without writing COM components for Windows Media Foundation in C++. The camera can be consumed by other applications relying on Windows Media Foundation to access a regular (hardware) camera. Some examples are Microsoft Teams or Skype.

## Requirements

Since the virtual camera API of Windows Media Foundation has been introduced more recently, using the camera requires Windows 11 (Build 22000 and up). The example application installed with the virtual camera allows you to check whether everything is working on your system. If it is, you can check out the source code in this repository to learn how to use it.

## Dependencies

Since the virtual camera is written using a combination of C++ and .NET, you will have to install the Visual C++ 14 Runtime as well as the .NET Framework 4.8. The bootstrapper application of the installer (setup.exe) should take care of detecting and installing the dependencies. If this does not work, you can download the runtime manually and use the .msi package to install the camera.

## Using the Example Camera App

Once you installed the virtual camera, there will be a shortcut on your desktop called "Virtual Camera". Use it to start the example camera app. Then tap start camera to install a virtual camera. Now you can open another application that leverages a camera feed and select the camera. Once the camera feed is shown, try changing the text in the camera app. To shut down the camera, press the stop button or simply close the camera app.

## Developing Custom Applications

The git repository contains a project for Visual Studio 2022 that demonstrates the APIs of the .NET assembly. It implements a simple animation that is displayed when the camera is started and used.

### Application Programming Interface

The application programming interface of the .NET assembly consists of 2 interfaces and 1 class:

- `VirtualCamera`: This class represents the virtual camera. To construct a camera, you must provide the width and height of the camera images in pixels and a `FrameGeneratorFactory`. Once the camera is instantiated, you can use the start and stop methods to install and remove the camera. Both methods return unsigned integers representing a HRESULT, meaning that 0 represents success and other values indicate certain failures. To translate the failures, you can use Microsoft's [documentation on COM error codes](https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-1).
- `FrameGeneratorFactory`: Classes implementing this interface are responsible for returning a `FrameGenerator` when a camera is used for the first time. Since the Media Foundation Frame Server Service is typically automatically restarted when a camera hangs or an error occurs, a single camera might repeatedly request a `FrameGenerator` (e.g., because the service was killed and restarted). 
- `FrameGenerator`: Classes implementing this interface are responsible for generating the images of the camera feed. Since Windows Media Foundation uses a pull model, the `FrameGenerator` gets called whenever a new frame is needed. When called the `FrameGenerator` should quickly create an image in RGB32 format and write its bytes to the BinaryWriter passed as a parameter. Note that the `FrameGenerator` MUST produce and write the correct number of bytes. A failure to do so will break the underlying protocol between the virtual camera and the .NET assembly. 

In addition, there are two classes that you can use to implement the interfaces:
- `SingletonFrameGeneratorFactory`: This class allows you to use a single instance as a `FrameGenerator`.
- `FrameGeneratorBase`: This class provides a helper method for synchronization.

The example console app in the repository implements a `FrameGeneratorFactory` and a `FrameGenerator` that render a simple circular progress animation. The main method instantiates and starts the camera waits for some user input and finally stops the camera again.

### Important Notes

To import the namespace of the virtual camera (`LocosLab.VirtualCamera`), you must add a reference to the assembly containing the virtual camera API to your project references. To do this open the project explorer and go to references, select add and then press the search button to navigate to the folder containing the assembly which should be `%windir%\Microsoft.NET\assembly\GAC_MSIL\LocosLab.VirtualCamera`. Then find the file called LocosLab.VirtualCamera.dll and press ok. 

When writing an application for "Any CPU", make sure to uncheck the 'prefer 32-bit' option in the build settings. Failure to do so will result in linking errors, since the .NET assembly will try to load a native library.

## Virtual Camera Architecture

The virtual camera consists of two components. The first is a DLL written in C++ that provides a COM component implementing a media source. The DLL can be registered in the registry to make the COM component available to the Frame Server Service used by Windows Media Foundation. This DLL also contains the code to register the media source as a virtual camera and to start and stop it. The second component is the .NET assembly that implements the API. 

When a camera is started through the .NET assembly, the assembly will search for the location of DLL with the media source in the Windows registry. If it can find the DLL, it will load it into the process and call the native function that starts the camera. This will trigger the Frame Server Service to load the COM component inside the DLL as well. Once the Frame Server Service is starting the camera, it will request frames from the .NET assembly.

Since the camera and the assembly reside in separate processes, it is neccessary to exchange the frames between the Frame Server Service and the application using some form of inter process communication. To do this, the virtual camera uses named pipes. Thereby, the assembly acts as server and the native DLL implements the client.

### Security Considerations

To receive the frames, the Frame Server Service must be able to access the named pipe opened by the application. For this to work, the application creates the pipe with access control settings that allow other users on the same machine to access the named pipe as well. Since there is no further authentication, this means that other users could potentially access the camera frames while the camera is installed and running. 

## References

During the development of the virtual camera, the following documentation was helpful:

- [Microsoft COM](https://learn.microsoft.com/en-us/windows/win32/com/the-component-object-model): This documentation provides a brief but complete description of Microsoft COM.
- [COM Error Codes](https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes): A list of the generic error codes returned by COM. This helps you to translate error codes to something more meaningful.
- [Windows Media Foundation](https://learn.microsoft.com/en-us/windows/win32/medfound/media-foundation-programming-guide): This documentation provides an overview of Windows Media Foundation.
- [Media Foundation Camera](https://learn.microsoft.com/en-us/windows/win32/api/mfvirtualcamera/nn-mfvirtualcamera-imfvirtualcamera): This documentation contains the API description for the virtual camera APIs available in Windows 11.

The following repositories contain code examples that implement media sources:

- [Windows Virtual Camera](https://github.com/microsoft/Windows-Camera/tree/master/Samples/VirtualCamera/): Code for different types of virtual cameras implemented in C++. The repository also contains a systray icon and an installer which was very helpful for me.
- [Windows Driver Examples](https://github.com/microsoft/Windows-driver-samples/tree/main/general/SimpleMediaSource/MediaSource): A media source implementation for mini drivers which used to be the way to implement virtual cameras before Windows 11 introduced a new API.
