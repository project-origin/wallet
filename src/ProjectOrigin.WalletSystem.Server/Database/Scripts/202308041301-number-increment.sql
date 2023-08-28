CREATE TABLE IF NOT EXISTS IdIncrements (
    Id uuid PRIMARY KEY,
    number INT NOT NULL
);

CREATE OR REPLACE FUNCTION IncrementNumberForId(
    IN in_id UUID,
    OUT out_number INT
)
LANGUAGE plpgsql
AS $$
BEGIN
    -- Fetch the current number for the given ID using FOR UPDATE to lock the row
    SELECT number INTO out_number FROM IdIncrements WHERE ID = in_id FOR UPDATE;

    IF NOT FOUND THEN
        -- If the ID is not found, insert a new row with number 1
        -- if a concurrent transaction has already inserted a row,
        -- the ON CONFLICT clause will increment the number and return it
        INSERT INTO IdIncrements (id, number)
        VALUES (in_id, 1)
        ON CONFLICT (ID) DO UPDATE SET number = IdIncrements.number + 1
        RETURNING number INTO out_number;
    ELSE
        -- If the ID is found, increment the number and update the row
        out_number := out_number + 1;
        UPDATE IdIncrements SET number = out_number WHERE ID = in_id;
    END IF;
END;
$$;
