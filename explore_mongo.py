# explore_mongo.py
from pymongo import MongoClient

client = MongoClient('mongodb://localhost:27017/')
print("Connected to MongoDB")

# List all databases
print("\n=== All Databases ===")
for db_name in client.list_database_names():
    print(f"  - {db_name}")

# Check each database's collections
for db_name in client.list_database_names():
    db = client[db_name]
    collections = db.list_collection_names()
    if collections:
        print(f"\n=== Database: {db_name} ===")
        for coll in collections:
            count = db[coll].count_documents({})
            print(f"  - {coll}: {count} documents")
            if count > 0:
                sample = db[coll].find_one()
                print(f"    Sample keys: {list(sample.keys())[:10]}")

client.close()