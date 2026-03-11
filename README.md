AI FAQ Processing API (FOR TESTING AZURE SERVICES PURPOSE)

This document provides an overview of the architecture, workflow, and endpoints implemented in the AllController class. 
The API processes uploaded PDFs, splits them into pages, extracts content using Azure Document Intelligence, and generates Q&A pairs using Azure OpenAI.

The API performs the following major tasks:

 - Upload and validate PDF files
 - Split PDFs into individual pages
 - Send each page to Azure Document Intelligence for text, table, and figure extraction
 - Render PDF pages into images and crop detected tables/figures
 - Aggregate extracted page data into JSON files
 - Generate Q&A pairs using Azure OpenAI with a structured prompt

For demo purpose, Please uses localhost:<port_number>/scalar/v1 to check API documentation. 
