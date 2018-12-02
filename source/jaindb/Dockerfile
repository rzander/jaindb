FROM microsoft/dotnet:2.1-aspnetcore-runtime
ARG source
WORKDIR /app
EXPOSE 5000:5000/tcp
EXPOSE 6379:6379/tcp
COPY ${source:-obj/Docker/publish} .
ENTRYPOINT ["dotnet", "jaindb.dll"]
RUN apt-get update

RUN cp /app/wwwroot/bin /usr/local -r
RUN rm -f -r /app/wwwroot/bin

#RUN apt-get install -y make gcc wget
#RUN apt-get install -y --no-install-recommends ca-certificates
#RUN mkdir -p /app/wwwroot/redis
#RUN wget -O redis.tar.gz http://download.redis.io/releases/redis-5.0.2.tar.gz
#RUN tar -xzf redis.tar.gz -C /app/wwwroot/redis --strip-components=1

#RUN grep -q '^#define CONFIG_DEFAULT_PROTECTED_MODE 1$' /app/wwwroot/redis/src/server.h
#RUN sed -ri 's!^(#define CONFIG_DEFAULT_PROTECTED_MODE) 1$!\1 0!' /app/wwwroot/redis/src/server.h
#RUN grep -q '^#define CONFIG_DEFAULT_PROTECTED_MODE 0$' /app/wwwroot/redis/src/server.h

#RUN make -C /app/wwwroot/redis distclean
#RUN make -C /app/wwwroot/redis install
#RUN rm -f /app/wwwroot/redis.tar.gz
#RUN rm -f /app/redis.tar.gz
#RUN mkdir -p /app/wwwroot/bin
#RUN cp /app/wwwroot/redis/src/redis-benchmark /app/wwwroot/bin
#RUN cp /app/wwwroot/redis/src/redis-check-aof /app/wwwroot/bin
#RUN cp /app/wwwroot/redis/src/redis-check-rdb /app/wwwroot/bin
#RUN cp /app/wwwroot/redis/src/redis-cli /app/wwwroot/bin
#RUN cp /app/wwwroot/redis/src/redis-sentinel /app/wwwroot/bin
#RUN cp /app/wwwroot/redis/src/redis-server /app/wwwroot/bin
##RUN rm -r /app/wwwroot/redis

#ENV localURL "http://localhost"
ENV WebPort 5000
ENV StartDelay 3000
ENV ReportUser "DEMO"
ENV ReportPW "password"
ENV DisableAuth 1
ENV ReadOnly 0
