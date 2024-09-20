FROM rabbitmq:4.0

RUN rabbitmq-plugins enable --offline rabbitmq_management

EXPOSE 15672
