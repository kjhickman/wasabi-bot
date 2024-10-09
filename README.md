# Wasabi bot

## Development

### Dependencies

- .NET 8 SDK
- AWS CLI
- CDKTF
- Docker
- [Task](https://github.com/go-task/task)

#### Local postgres container command:
```
docker run -d --name fake-neon -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=defaultdb -p 5432:5432 postgres:latest
```
