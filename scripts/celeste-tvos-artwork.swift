#!/usr/bin/env swift

import AppKit
import Foundation
import ImageIO

enum ArtworkError: Error, CustomStringConvertible {
    case message(String)

    var description: String {
        switch self {
        case .message(let value): return value
        }
    }
}

struct Canvas {
    let width: Int
    let height: Int
}

func loadImage(_ path: String, requireTransparency: Bool) throws -> CGImage {
    let url = URL(fileURLWithPath: path)
    guard let source = CGImageSourceCreateWithURL(url as CFURL, nil) else {
        throw ArtworkError.message("not a readable image: \(path)")
    }
    var bestIndex = 0
    var bestArea = 0
    for index in 0..<CGImageSourceGetCount(source) {
        guard let properties = CGImageSourceCopyPropertiesAtIndex(source, index, nil) as? [CFString: Any],
              let width = properties[kCGImagePropertyPixelWidth] as? Int,
              let height = properties[kCGImagePropertyPixelHeight] as? Int else { continue }
        if width * height > bestArea { bestArea = width * height; bestIndex = index }
    }
    guard let image = CGImageSourceCreateImageAtIndex(source, bestIndex, nil) else {
        throw ArtworkError.message("image has no readable frame: \(path)")
    }
    guard image.width > 0, image.height > 0 else {
        throw ArtworkError.message("image has no pixels: \(path)")
    }
    if requireTransparency {
        let alphaInfo = image.alphaInfo
        guard alphaInfo != .none && alphaInfo != .noneSkipFirst && alphaInfo != .noneSkipLast else {
            throw ArtworkError.message("Celeste launcher icon must contain a meaningful alpha channel")
        }
        let width = image.width
        let height = image.height
        var pixels = [UInt8](repeating: 0, count: width * height * 4)
        guard let context = CGContext(
            data: &pixels,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: width * 4,
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ) else {
            throw ArtworkError.message("could not inspect Celeste launcher icon alpha")
        }
        context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
        var transparent = false
        var visible = false
        for offset in stride(from: 3, to: pixels.count, by: 4) {
            let alpha = pixels[offset]
            transparent = transparent || alpha < 250
            visible = visible || alpha > 5
            if transparent && visible { break }
        }
        guard transparent && visible else {
            throw ArtworkError.message("Celeste launcher icon alpha must contain transparent and visible pixels")
        }
    }
    return image
}

func writePNG(_ image: CGImage, to path: String) throws {
    let url = URL(fileURLWithPath: path)
    guard let destination = CGImageDestinationCreateWithURL(url as CFURL, "public.png" as CFString, 1, nil) else {
        throw ArtworkError.message("could not create PNG: \(path)")
    }
    CGImageDestinationAddImage(destination, image, [kCGImagePropertyPNGInterlaceType: 0] as CFDictionary)
    guard CGImageDestinationFinalize(destination) else {
        throw ArtworkError.message("could not write PNG: \(path)")
    }
}

func render(canvas: Canvas, source: CGImage?, mode: String, blackBackground: Bool, safeFraction: CGFloat = 1.0) throws -> CGImage {
    let colorSpace = CGColorSpace(name: CGColorSpace.sRGB) ?? CGColorSpaceCreateDeviceRGB()
    guard let context = CGContext(
        data: nil,
        width: canvas.width,
        height: canvas.height,
        bitsPerComponent: 8,
        bytesPerRow: canvas.width * 4,
        space: colorSpace,
        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
    ) else {
        throw ArtworkError.message("could not create \(canvas.width)x\(canvas.height) canvas")
    }
    context.interpolationQuality = .high
    let bounds = CGRect(x: 0, y: 0, width: canvas.width, height: canvas.height)
    if blackBackground {
        context.setFillColor(CGColor(gray: 0, alpha: 1))
        context.fill(bounds)
    } else {
        context.clear(bounds)
    }
    if let source {
        let sourceSize = CGSize(width: source.width, height: source.height)
        let available = CGSize(width: CGFloat(canvas.width) * safeFraction, height: CGFloat(canvas.height) * safeFraction)
        let scale: CGFloat
        if mode == "fill" {
            scale = max(CGFloat(canvas.width) / sourceSize.width, CGFloat(canvas.height) / sourceSize.height)
        } else {
            scale = min(available.width / sourceSize.width, available.height / sourceSize.height)
        }
        let drawn = CGSize(width: sourceSize.width * scale, height: sourceSize.height * scale)
        let rect = CGRect(
            x: (CGFloat(canvas.width) - drawn.width) / 2,
            y: (CGFloat(canvas.height) - drawn.height) / 2,
            width: drawn.width,
            height: drawn.height
        )
        context.draw(source, in: rect)
    }
    guard let output = context.makeImage() else {
        throw ArtworkError.message("could not finish rendered image")
    }
    return output
}

func usage() {
    print("Usage: celeste-tvos-artwork.swift --icon IMAGE --splash PNG --output DIR")
}

var iconPath: String?
var splashPath: String?
var outputPath: String?
var index = 1
while index < CommandLine.arguments.count {
    let argument = CommandLine.arguments[index]
    if argument == "--help" || argument == "-h" {
        usage()
        exit(0)
    }
    guard index + 1 < CommandLine.arguments.count else {
        fputs("error: \(argument) requires a value\n", stderr)
        exit(2)
    }
    let value = CommandLine.arguments[index + 1]
    switch argument {
    case "--icon": iconPath = value
    case "--splash": splashPath = value
    case "--output": outputPath = value
    default:
        fputs("error: unknown option: \(argument)\n", stderr)
        exit(2)
    }
    index += 2
}

do {
    guard let iconPath, let splashPath, let outputPath else {
        usage()
        throw ArtworkError.message("--icon, --splash, and --output are required")
    }
    let icon = try loadImage(iconPath, requireTransparency: true)
    let splash = try loadImage(splashPath, requireTransparency: false)
    let output = URL(fileURLWithPath: outputPath)
    try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)

    let appCanvases: [(String, Canvas)] = [
        ("small-1x", Canvas(width: 400, height: 240)),
        ("small-2x", Canvas(width: 800, height: 480)),
        ("store-1x", Canvas(width: 1280, height: 768))
    ]
    for (name, canvas) in appCanvases {
        try writePNG(render(canvas: canvas, source: nil, mode: "fit", blackBackground: true), to: output.appendingPathComponent("\(name)-back.png").path)
        try writePNG(render(canvas: canvas, source: icon, mode: "fit", blackBackground: false, safeFraction: 0.46), to: output.appendingPathComponent("\(name)-front.png").path)
    }
    for (name, canvas) in [
        ("top-shelf-1x", Canvas(width: 1920, height: 720)),
        ("top-shelf-2x", Canvas(width: 3840, height: 1440)),
        ("top-shelf-wide-1x", Canvas(width: 2320, height: 720)),
        ("top-shelf-wide-2x", Canvas(width: 4640, height: 1440))
    ] {
        try writePNG(render(canvas: canvas, source: splash, mode: "fill", blackBackground: true), to: output.appendingPathComponent("\(name).png").path)
    }
} catch {
    fputs("error: \(error)\n", stderr)
    exit(1)
}
