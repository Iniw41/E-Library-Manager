# E-Library-Manager

graph TD
  ProgramClass["Program"]
  ProgramMain["Program.Main()"]
  ProgramMainmenuDisplay["Program.mainmenu_display_login()"]
  ProgramMainmenuSelection["Program.mainmenu_selection()"]
  ProgramAdminSession["Program.AdminSession()"]
  ProgramUserSession["Program.UserSession()"]
  ProgramPromptLogin["Program.PromptLogin(bool isAdmin)"]
  ProgramReadPassword["Program.ReadPasswordMasked()"]

  UsersDisplayMenuClass["UsersDisplayMenu"]
  UsersDisplayMenu_AdminMenu["UsersDisplayMenu.AdminMenu()"]
  UsersDisplayMenu_UserMenu["UsersDisplayMenu.UserMenu()"]
  UsersDisplayMenu_BookMenu["UsersDisplayMenu.BookMenu()"]

  StyleConsPrintClass["StyleConsPrint"]
  StyleConsPrint_WriteCentered["StyleConsPrint.WriteCentered(string)"]
  StyleConsPrint_WriteBottom["StyleConsPrint.WriteBottom(string)"]

  AllUsersClass["AllUsers\n(id, username, password, fullname, age, email)"]
  AdminClass["Admin\n- Login(username,password)\n- AddUser()\n- RemoveUser()\n- DisplayInfo()"]
  StandardUserClass["StandardUser\n- Login(username,password)\n- DisplayInfo()\n- BorrowBook()\n- ReturnBook()\n- ViewBorrowedBooks()\n- ViewBoookInfo()\n- ShowTopBorrowers()\n- ShowTopBookCompleationists()"]

  UserRepositoryClass["UserRepository\n- GetAllUsers()\n- GetTop10ByCompleted()\n- GetTop10ByBorrowed()"]
  DatabaseClass["Database\n(tables: Accounts, BorrowedRecords, CompletedRecords)"]
  UserStatisticsClass["UserStatistics\n- GetTop10ByCompleted()\n- GetTop10ByBorrowed()\n- DisplayTop10Completed()\n- DisplayTop10Borrowed()"]

  %% Program structure & calls
  ProgramClass --> ProgramMain
  ProgramMain --> ProgramMainmenuSelection
  ProgramMainmenuSelection --> ProgramMainmenuDisplay
  ProgramMainmenuSelection --> ProgramPromptLogin
  ProgramMainmenuSelection --> ProgramAdminSession
  ProgramMainmenuSelection --> ProgramUserSession

  ProgramPromptLogin --> ProgramReadPassword
  ProgramPromptLogin --> AdminClass
  ProgramPromptLogin --> StandardUserClass
  ProgramPromptLogin --> StyleConsPrint_WriteCentered

  ProgramMainmenuDisplay --> StyleConsPrint_WriteCentered
  ProgramMain --> StyleConsPrint_WriteCentered
  ProgramAdminSession --> StyleConsPrint_WriteCentered
  ProgramUserSession --> StyleConsPrint_WriteCentered

  %% Admin/User session -> menus
  ProgramAdminSession --> UsersDisplayMenu_AdminMenu
  ProgramUserSession --> UsersDisplayMenu_UserMenu
  ProgramUserSession --> UsersDisplayMenu_BookMenu

  UsersDisplayMenuClass --> UsersDisplayMenu_AdminMenu
  UsersDisplayMenuClass --> UsersDisplayMenu_UserMenu
  UsersDisplayMenuClass --> UsersDisplayMenu_BookMenu

  UsersDisplayMenu_AdminMenu --> StyleConsPrint_WriteCentered
  UsersDisplayMenu_UserMenu --> StyleConsPrint_WriteCentered
  UsersDisplayMenu_BookMenu --> StyleConsPrint_WriteCentered

  %% Inheritance
  AdminClass -.->|inherits| AllUsersClass
  StandardUserClass -.->|inherits| AllUsersClass

  %% Top-10 statistics flow (assumed)
  StandardUserClass --> UserStatisticsClass
  AdminClass --> UserStatisticsClass
  UsersDisplayMenu_AdminMenu -->|option: show top-10| UserStatisticsClass
  UsersDisplayMenu_UserMenu -->|option: show top-10| UserStatisticsClass

  UserStatisticsClass --> UserRepositoryClass
  UserRepositoryClass --> DatabaseClass
  UserStatisticsClass --> UsersDisplayMenu_AdminMenu
  UserStatisticsClass --> UsersDisplayMenu_UserMenu
  UserStatisticsClass --> StyleConsPrint_WriteCentered

  %% StyleConsPrint composition
  StyleConsPrintClass --> StyleConsPrint_WriteCentered
  StyleConsPrintClass --> StyleConsPrint_WriteBottom
