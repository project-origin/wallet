CREATE TABLE job_execution_history (
    job_name VARCHAR(512) PRIMARY KEY,
    last_execution_time timestamp with time zone NOT NULL
);
