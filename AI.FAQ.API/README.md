AI FAQ Processing API

This document provides an overview of the architecture, workflow, and endpoints implemented in the AllController class. The API processes uploaded PDFs, splits them into pages, extracts content using Azure Document Intelligence, and generates Q&A pairs using Azure OpenAI.

📌 Overview

The API performs the following major tasks:

Upload and validate PDF files

Split PDFs into individual pages

Send each page to Azure Document Intelligence for text, table, and figure extraction

Render PDF pages into images and crop detected tables/figures

Aggregate extracted page data into JSON files

Generate Q&A pairs using Azure OpenAI with a structured prompt

🗂️ Project Workflow

1. Upload & Split PDF

Endpoint: POST /api/all/1/upload-and-split

This endpoint:

Validates file size, extension, and MIME type

Uploads the original PDF to Azure Blob Storage

Splits the PDF into individual pages using PdfSharpCore

Saves each page as a separate PDF under pdfsplits/{generatedId}/page-X.pdf

2. Send Pages to Azure Document Intelligence

Endpoint: GET /api/all/2/send-to-document-intelligent

This step:

Iterates through all split pages

Generates a SAS URL for each page

Sends the PDF bytes to Azure Document Intelligence (prebuilt-layout model)

Saves the full JSON response to disk

Renders the PDF page into an image using PdfPig + SkiaSharp

Crops detected tables and figures using bounding polygons

Saves cropped images under {folder}_images/page-X/

3. Concatenate Page Data

Endpoint: GET /api/all/3/read

This endpoint:

Reads all saved DI JSON files

Extracts text, figure count, and table count

Produces two JSON files:

all_pages_data.json — all pages

pages_with_figures_tables.json — only pages containing tables/figures

4. Read All Page Data

Endpoint: GET /api/all/3.5/read-all-pages

Returns the contents of all_pages_data.json.

5. Generate Q&A Pairs Using Azure OpenAI

Endpoint: GET /api/all/4/open-ai-generate-qa

This step:

Loads all_pages_data.json

Processes pages in batches of 5

Sends text batches to Azure OpenAI with a structured JSON extraction prompt

Handles prefix/suffix context between batches

Saves final Q&A pairs to qa-pairs.json

🧠 Key Technologies Used

Component

Purpose

Azure Blob Storage

Store uploaded PDFs and split pages

PdfSharpCore

Split PDFs into individual pages

PdfPig + SkiaSharp

Render PDF pages and crop tables/figures

Azure Document Intelligence

Extract text, tables, figures, and layout

Azure OpenAI (Chat Completions)

Generate structured Q&A pairs

ASP.NET Core Web API

API framework

⚙️ Configuration Keys

The following keys must be present in appsettings.json:

{
  "AzureBlobStorage": {
    "ConnectionString": "...",
    "UploadContainerName": "pdfuploads",
    "SplitContainerName": "pdfsplits"
  },
  "DocumentIntelligence": {
    "Endpoint": "...",
    "Key": "..."
  },
  "AzureOpenAI": {
    "Endpoint": "...",
    "Key": "...",
    "Deployment": "gpt-model-name"
  },
  "DIFolder": {
    "FolderName": "di-output",
    "CurrentTarget": "..."
  }
}

📁 Output File Structure

/Data
 ├── {folder}/
 │    ├── diResult_1.json
 │    ├── diResult_2.json
 │    └── ...
 ├── {folder}_images/
 │    └── page-1/
 │         ├── table_0.png
 │         └── figure_0.png
 ├── all_pages_data.json
 ├── pages_with_figures_tables.json
 └── qa-pairs.json

🧩 Helper Functions

GenerateNewFileName() — Creates a timestamped random filename

RenderPdfToImage() — Converts a PDF page to a bitmap

CropAndSave() — Crops detected regions using polygon coordinates

PolygonToRect() — Converts polygon points into a bounding rectangle

🚀 Summary

This API provides a complete pipeline for:

PDF ingestion

Page-level extraction

Layout analysis

Image cropping

Q&A generation

It is designed for enterprise-grade document processing, especially FAQ extraction from large PDFs.