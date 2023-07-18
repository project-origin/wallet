begin;
    ALTER TABLE Slices ADD COLUMN SliceState integer NULL;
    UPDATE Slices SET SliceState = 1;
    ALTER TABLE Slices ALTER COLUMN SliceState SET NOT NULL;
commit;
