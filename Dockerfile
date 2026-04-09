FROM ubuntu:22.04

# Evita preguntas interactivas
ENV DEBIAN_FRONTEND=noninteractive

# Instalar librerías necesarias para Unity
RUN apt-get update && apt-get install -y \
    libglu1-mesa \
    libxcursor1 \
    libxrandr2 \
    libxinerama1 \
    libxi6 \
    && rm -rf /var/lib/apt/lists/*

# Crear carpeta del juego
WORKDIR /app

# Copiar build
COPY Builds/LinuxServer/ .

# Dar permisos de ejecución
RUN chmod +x /app/MyGameServer.x86_64

# Puerto (Netcode usa esto normalmente)
EXPOSE 7777/udp

# Ejecutar servidor
CMD ["./MyGameServer.x86_64", "-batchmode", "-nographics"]
