CREATE INDEX idx_interaction_record_username ON interaction_record (username);
CREATE INDEX idx_interaction_record_data_name ON interaction_record ((data->>'name'));
