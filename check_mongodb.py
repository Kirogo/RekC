# check_mongodb.py
from pymongo import MongoClient

# Connect to MongoDB
client = MongoClient('mongodb://localhost:27017/')
print("Connected to MongoDB")

# List all databases
print("\n=== Databases ===")
for db_name in client.list_database_names():
    print(f"  - {db_name}")

# Check the 'test' database (or whatever your DB name is)
db = client['test']  # Change this to your actual database name if different

print(f"\n=== Collections in 'test' database ===")
for collection_name in db.list_collection_names():
    count = db[collection_name].count_documents({})
    print(f"  - {collection_name}: {count} documents")
    
    # Show first document if exists
    if count > 0:
        sample = db[collection_name].find_one()
        print(f"    Sample fields: {list(sample.keys())[:10]}")
        print()

client.close()