# entity framework

### add new migration changes (code change/schema change)

```sh
# dotnet-ef migrations add
# VS Package Manager Console
Add-Migration AddMatchingRecord
Update-Database

Add-Migration MatchingRecordRating
Update-Database

Add-Migration ChatSchema
Update-Database
```

## for ubuntu

```sh
# once
dotnet new tool-manifest
dotnet tool install --local dotnet-ef --version 8.0.17
# if only paste content of another project `.config/dotnet-tools.json`
dotnet tool restore

# create new database
dotnet tool run dotnet-ef migrations add InitialCreate
dotnet tool run dotnet-ef database update

# delete database

## install packages - ubuntu 20.04
curl https://packages.microsoft.com/keys/microsoft.asc | sudo tee /etc/apt/trusted.gpg.d/microsoft.asc
curl https://packages.microsoft.com/config/ubuntu/20.04/prod.list | sudo tee /etc/apt/sources.list.d/mssql-release.list
sudo apt-get update
sudo apt-get install mssql-tools mssql-tools18 unixodbc-dev

## one line delete command
/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P '123456' -Q "DROP DATABASE PRN232_Library;"
```

# publish artifact

```cmd
dotnet publish --os linux --self-contained true --interactive --configuration Release --output publish
```

# `scp` command

```sh
scp [options] source destination

scp "C:\artifact" user@hostname:/root/data
```

# deploy note

1. push artifact
2. `chmod +x`
3. modify `appsettings.json` to config `0.0.0.0:443` and SSL certificate location
4. TODO change JWT secret
5. TODO make database backup/migration

# update database schema

```sh
# not usable with SQLite
dotnet-ef migrations script --output "publish/migration_update.sql" --idempotent
```
