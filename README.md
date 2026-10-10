# Overview

I created FileSpace to strengthen my skills in building web applications with C# and ASP.NET Core. My goal was to learn how to connect user authentication, database operations, and local file storage in a working application while keeping each user's files private.

FileSpace is a web application that allows users to register, log in, and manage their own files. It includes a personal Dashboard, an Upload page, and a My Files page. Users can upload several files at once, search by file name, filter by file type, download files, and delete files they no longer need. The dashboard displays the number of uploaded files, storage usage, and recent uploads.

The application stores files locally and saves user accounts and file metadata in SQLite through Entity Framework Core. ASP.NET Core Identity handles authentication, and file operations check ownership so users can access only their own files. Uploads validate allowed file extensions and file sizes, and the interface displays success and error messages.

My purpose for creating this software was to practice the Model-View-Controller pattern, work with a relational database, and understand how to handle file uploads and authenticated requests. I also wanted to improve my ability to build a responsive interface and verify application behavior through end-to-end tests.

[Software Demo Video](https://youtu.be/bzE-Y7ZJ0Ic?si=_cmweJVQLKYAMeFV)

Replace the placeholder above with a link to a one-minute YouTube demonstration showing the application running and a walkthrough of the code.

# Development Environment

The application was developed using the .NET 10 SDK and ASP.NET Core MVC. Entity Framework Core connects the application to SQLite, and ASP.NET Core Identity manages user registration, password hashing, and login. The .NET CLI builds and runs the application, and a Python smoke-test script checks the main workflows, upload validation, and protection against access to another user's files.

C# is the primary programming language for the controllers, models, authentication, and database operations. Razor views combine HTML with C# to display dynamic information. CSS provides the responsive layout and styling, while JavaScript supports drag-and-drop uploads, selected-file previews, and deletion confirmations.

To run the application locally, install the .NET 10 SDK and execute the following command from the project directory:

```sh
dotnet run
```

Open **http://localhost:5080** in a browser. The application creates its SQLite database and local upload directory automatically on first startup.

# Useful Websites

* [ASP.NET Core MVC Documentation](https://learn.microsoft.com/en-us/aspnet/core/mvc/overview)
* [File Uploads in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads)
* [ASP.NET Core Identity Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity)
* [Entity Framework Core SQLite Provider](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)
* [C# Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/)
