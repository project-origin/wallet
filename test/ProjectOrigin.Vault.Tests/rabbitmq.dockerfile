FROM rabbitmq:4.1

RUN rabbitmq-plugins enable --offline rabbitmq_management

EXPOSE 15672
