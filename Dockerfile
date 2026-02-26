# ============================================
# Stage 1: Build the .NET application
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY FinanceApi.csproj .
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# ============================================
# Stage 2: Runtime with Python support
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Install Python and pip for stock data scripts
RUN apt-get update && \
    apt-get install -y --no-install-recommends python3 python3-pip python3-venv && \
    rm -rf /var/lib/apt/lists/*

# Create a virtual environment for Python packages
RUN python3 -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Copy and install Python dependencies
COPY scripts/requirements.txt /app/scripts/requirements.txt
RUN pip install --no-cache-dir -r /app/scripts/requirements.txt

# Copy Python scripts
COPY scripts/ /app/scripts/

# Copy published .NET app
COPY --from=build /app/publish .

# Create directory for SQLite database persistence
# Symlink so Python scripts (which look for ../dividends.db relative to scripts/)
# find the database that .NET stores in /app/data/
RUN mkdir -p /app/data && \
    ln -s /app/data/dividends.db /app/dividends.db

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 5000

ENTRYPOINT ["dotnet", "FinanceApi.dll"]
