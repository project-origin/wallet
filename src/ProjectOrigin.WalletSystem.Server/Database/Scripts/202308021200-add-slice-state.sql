begin;
    ALTER TABLE Slices ADD COLUMN SliceState integer NULL;
    UPDATE Slices SET SliceState = 1 WHERE SliceState IS NULL;
    ALTER TABLE Slices ALTER COLUMN SliceState SET NOT NULL;
commit;
