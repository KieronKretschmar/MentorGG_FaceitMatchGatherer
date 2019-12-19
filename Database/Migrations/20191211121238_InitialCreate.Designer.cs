﻿// <auto-generated />
using System;
using Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Database.Migrations
{
    [DbContext(typeof(FaceitContext))]
    [Migration("20191211121238_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("Entities.Models.Match", b =>
                {
                    b.Property<string>("FaceitMatchId")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.HasKey("FaceitMatchId");

                    b.ToTable("Matches");
                });

            modelBuilder.Entity("Entities.Models.User", b =>
                {
                    b.Property<long>("SteamId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    b.Property<string>("FaceitId")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<int>("FaceitMembership")
                        .HasColumnType("int");

                    b.Property<string>("FaceitName")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<DateTime>("LastChecked")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("RefreshToken")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("Token")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<DateTime>("TokenExpires")
                        .HasColumnType("datetime(6)");

                    b.HasKey("SteamId");

                    b.ToTable("Users");
                });
#pragma warning restore 612, 618
        }
    }
}
