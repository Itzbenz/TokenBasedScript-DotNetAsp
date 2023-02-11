systemctl stop kestrel-tokenbasedscript
dotnet publish --configuration Release
systemctl start kestrel-tokenbasedscript 
