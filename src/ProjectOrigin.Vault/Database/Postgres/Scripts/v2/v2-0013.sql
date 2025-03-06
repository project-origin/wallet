CREATE TABLE IF NOT EXISTS OutboxMessages
(
    id
    uuid
    NOT
    NULL
    PRIMARY
    KEY,
    message_type
    VARCHAR
(
    250
) NOT NULL,
    json_payload TEXT NOT NULL,
    created timestamp with time zone NOT NULL
                          );
