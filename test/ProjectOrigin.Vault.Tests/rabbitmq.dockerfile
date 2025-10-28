FROM rabbitmq:4.2

RUN rabbitmq-plugins enable --offline rabbitmq_management

EXPOSE 15672
