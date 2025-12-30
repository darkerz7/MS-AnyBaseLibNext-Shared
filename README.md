# MS-AnyBaseLibNext-Shared
Just an easy-connecting database api for SQLite/MySQL/PostgreSQL. Based on: [AnyBaseLibCS2](https://github.com/NickFox007/AnyBaseLibCS2)

## Example:
### Add the dependency AnyBaseLibNext to your project:
```
//IAnyBaseNext AnyDB = CAnyBaseNext.Base("sqlite");
IAnyBaseNext AnyDB = CAnyBaseNext.Base("mysql");
//IAnyBaseNext AnyDB = CAnyBaseNext.Base("postgre");
AnyDB.Set(sDBNameOrFilename, sDBHost, sDBUser, sDBPassword);
...
AnyDB.QueryAsync(sQuery, new List<string>([sArg1, sArg2...]), (_) => { MyActions...}), bNon_Query, bImportant);
```
