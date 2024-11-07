CREATE PROCEDURE expire_slices(older_than_date timestamp with time zone, expire_state_int integer, available_state_int integer)
AS $$
BEGIN
    UPDATE wallet_slices ws
    SET state = expire_state_int
    FROM certificates c
    WHERE ws.certificate_id = c.id
      AND ws.registry_name = c.registry_name
      AND ws.state = available_state_int
      AND c.end_date < older_than_date;
END;
$$ language 'plpgsql';
